using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Operations_Control.Motion;

namespace Operations_Control.Views
{
    public sealed partial class DiagnosticsView
    {
        private readonly NumericUpDown analysisTolerance = Number(0, 1000000000, 10);
        private readonly Label errorMeanValue = MetricValue(), errorRmsValue = MetricValue(), errorPeakValue = MetricValue();
        private readonly Label errorPeakToPeakValue = MetricValue(), errorP95Value = MetricValue(), errorExceededValue = MetricValue();
        private readonly Label errorScoreValue = new Label { Dock = DockStyle.Fill, Text = "—", TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Microsoft YaHei UI", 23, FontStyle.Bold), ForeColor = Color.SteelBlue };
        private readonly Label analysisCoverage = new Label { AutoSize = true, Margin = new Padding(12, 8, 0, 0) };
        private readonly Label analysisMethod = new Label { Dock = DockStyle.Fill, Padding = new Padding(6, 2, 0, 0), Text = "按实际时间加权；相邻有效采样间线性插值，不跨断点。\r\n评分仅供相同容差下的相对比较，不代表设备精度合格判定。" };
        private readonly Timer historyRangeTimer = new Timer { Interval = 200 };
        private readonly ToolTip analysisTip = new ToolTip { AutoPopDelay = 20000 };
        private GroupBox analysisPanel;
        private TableLayoutPanel axisLayout;
        private Panel axisViewport;
        private DiagnosticErrorResult historyAnalysis;
        private bool analysisRunning, analysisPending;
        private int analysisVersion;

        private static Label MetricValue()
        { return new Label { Dock = DockStyle.Fill, Text = "—", Padding = new Padding(6, 0, 0, 0), Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold) }; }
        private Control CreateAnalysisPanel()
        {
            analysisPanel = new GroupBox { Text = "所选电机与时间段 · 偏差分析", Dock = DockStyle.Fill, Visible = false, Padding = new Padding(6) };
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            FlowLayoutPanel settings = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
            Field(settings, "评分容差 ct（0 = 未设置）", analysisTolerance); settings.Controls.Add(analysisCoverage);
            TableLayoutPanel content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75)); content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            TableLayoutPanel metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
            for (int i = 0; i < 3; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            string[] titles = { "平均误差 / ct", "RMS 误差 / ct", "最大绝对误差 / ct · 发生时间", "误差峰峰值 / ct", "绝对误差 P95 / ct", "超差时间 / s · 有效时长占比" };
            Label[] values = { errorMeanValue, errorRmsValue, errorPeakValue, errorPeakToPeakValue, errorP95Value, errorExceededValue };
            for (int i = 0; i < values.Length; i++)
            {
                Panel cell = new Panel { Dock = DockStyle.Fill };
                cell.Controls.Add(values[i]); cell.Controls.Add(new Label { Dock = DockStyle.Top, Height = 23, Text = titles[i], Padding = new Padding(6, 2, 0, 0) });
                metrics.Controls.Add(cell, i % 3, i / 3);
            }
            TableLayoutPanel score = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12, 0, 0, 0) };
            score.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            score.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); score.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); score.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            score.Controls.Add(new Label { Text = "综合误差评分 / 100", Dock = DockStyle.Fill }, 0, 0); score.Controls.Add(errorScoreValue, 0, 1);
            LinkLabel formula = new LinkLabel { Text = "越高误差越小 · 查看公式", Dock = DockStyle.Fill };
            formula.LinkClicked += delegate { MessageBox.Show(this, DiagnosticErrorAnalysis.ScoreFormula + "\r\n\r\nT 为评分容差（ct），超差比例取 0～1。\r\n权重：RMS 40%、P95 25%、最大绝对误差 15%、超差比例 20%。\r\n这是相对比较分数，不是行业标准或设备合格判定。", "综合误差评分", MessageBoxButtons.OK, MessageBoxIcon.Information); };
            score.Controls.Add(formula, 0, 2); analysisTip.SetToolTip(errorScoreValue, DiagnosticErrorAnalysis.ScoreFormula);
            content.Controls.Add(metrics, 0, 0); content.Controls.Add(score, 1, 0);
            layout.Controls.Add(settings, 0, 0); layout.Controls.Add(content, 0, 1); layout.Controls.Add(analysisMethod, 0, 2); analysisPanel.Controls.Add(layout);
            analysisTolerance.ValueChanged += delegate { if (reviewing && !loadingHistory) RequestHistoryAnalysis(); };
            reviewStart.ValueChanged += delegate { SyncHistoryRangeSelector(); ScheduleHistoryRange(reviewStart, EventArgs.Empty); };
            reviewEnd.ValueChanged += delegate { SyncHistoryRangeSelector(); ScheduleHistoryRange(reviewEnd, EventArgs.Empty); };
            historyRangeTimer.Tick += delegate { historyRangeTimer.Stop(); if (reviewing && !loadingHistory) WithRecordingError(delegate { DrawHistory(); }); };
            Disposed += delegate { historyRangeTimer.Dispose(); analysisTip.Dispose(); };
            return analysisPanel;
        }
        private void ScheduleHistoryRange(object sender, EventArgs e)
        {
            if (!reviewing || loadingHistory) return;
            historyRangeTimer.Stop(); historyRangeTimer.Start(); InvalidateHistoryAnalysis("时间段已变更，正在更新…");
        }
        private void SetAnalysisVisible(bool visible)
        {
            analysisPanel.Visible = visible; axisLayout.RowStyles[2].Height = visible ? 260 : 0;
            axisLayout.Height = Math.Max(visible ? 1150 : 890, axisViewport.ClientSize.Height);
        }
        private void InvalidateHistoryAnalysis(string reason)
        {
            analysisVersion++; analysisPending = false; historyAnalysis = null;
            foreach (Label label in new Label[] { errorMeanValue, errorRmsValue, errorPeakValue, errorPeakToPeakValue, errorP95Value, errorExceededValue, errorScoreValue }) label.Text = "—";
            analysisCoverage.Text = reason; UpdateRecordingButtons();
        }
        private void RequestHistoryAnalysis()
        {
            if (historyRangeTimer.Enabled) return;
            InvalidateHistoryAnalysis("正在计算所选时段…");
            if (!reviewing || history == null || reviewMotor.SelectedItem == null || reviewEnd.Value <= reviewStart.Value) return;
            analysisPending = true; StartPendingAnalysis();
        }
        private void StartPendingAnalysis()
        {
            if (analysisRunning || !analysisPending || !reviewing) return;
            analysisRunning = true; analysisPending = false;
            int version = analysisVersion, selected = (int)reviewMotor.SelectedItem;
            double start = (double)reviewStart.Value * 1000, end = (double)reviewEnd.Value * 1000;
            double? tolerance = analysisTolerance.Value > 0 ? (double?)analysisTolerance.Value : null;
            Operations_Control.Models.DiagnosticRecording recording = history;
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs args) { args.Result = DiagnosticErrorAnalysis.Analyze(recording, selected, start, end, tolerance); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs args)
            {
                Action completed = delegate
                {
                    worker.Dispose(); if (IsDisposed || Disposing) return;
                    analysisRunning = false;
                    if (version == analysisVersion && reviewing)
                    {
                        if (args.Error != null) InvalidateHistoryAnalysis("分析失败：" + args.Error.Message);
                        else { DiagnosticErrorResult result = (DiagnosticErrorResult)args.Result; DisplayHistoryAnalysis(result); historyAnalysis = result; }
                    }
                    StartPendingAnalysis(); UpdateRecordingButtons();
                };
                if (IsDisposed || Disposing) { worker.Dispose(); return; }
                if (InvokeRequired) { try { BeginInvoke(completed); } catch (InvalidOperationException) { worker.Dispose(); } }
                else completed();
            };
            worker.RunWorkerAsync();
        }
        private static string Metric(double value) { return value.ToString("0.###"); }
        private void DisplayHistoryAnalysis(DiagnosticErrorResult result)
        {
            if (result == null) return;
            analysisPanel.Text = "电机 " + reviewMotor.SelectedItem + " · " + Metric(result.StartMs / 1000) + "～" + Metric(result.EndMs / 1000) + " s · 偏差分析";
            if (!result.HasData)
            { analysisCoverage.Text = "数据不足：需要至少一个连续的有效闭环采样区间。"; errorScoreValue.Text = "无法评分"; return; }
            errorMeanValue.Text = Metric(result.Mean); errorRmsValue.Text = Metric(result.Rms);
            errorPeakValue.Text = Metric(result.MaximumAbsolute) + "\r\n@ " + Metric(result.MaximumTimeMs / 1000) + " s";
            errorPeakToPeakValue.Text = Metric(result.PeakToPeak); errorP95Value.Text = Metric(result.AbsoluteP95);
            errorExceededValue.Text = result.ExceededRatio.HasValue ? Metric(result.ExceededDurationMs.Value / 1000) + " s / " + (100 * result.ExceededRatio.Value).ToString("0.##") + "%" : "未设置容差";
            errorScoreValue.Text = result.Score.HasValue ? result.Score.Value.ToString("0.0") : "未设置容差";
            analysisCoverage.Text = "有效 " + Metric(result.ValidDurationMs / 1000) + " s / 所选 " + Metric((result.EndMs - result.StartMs) / 1000) + " s；覆盖率 " + (100 * result.Coverage).ToString("0.##") + "%";
            analysisTip.SetToolTip(analysisCoverage, "缺帧、超过 1 秒的间隔、未使能、开环或缺失误差不参与统计；分数只评价有效时段。最大值时间以 CSV 开始时刻为零点。");
        }
        private string AnalysisExportText()
        {
            DiagnosticErrorResult result = historyAnalysis;
            if (result == null) return "偏差分析尚未完成。";
            if (!result.HasData) return "偏差分析：连续有效数据不足，无法统计或评分。";
            return "平均误差 " + Metric(result.Mean) + " ct；RMS " + Metric(result.Rms) + " ct；最大绝对误差 " + Metric(result.MaximumAbsolute) + " ct @ " + Metric(result.MaximumTimeMs / 1000) + " s\r\n" +
                "峰峰值 " + Metric(result.PeakToPeak) + " ct；绝对误差 P95 " + Metric(result.AbsoluteP95) + " ct；超差 " + errorExceededValue.Text + "\r\n" +
                "综合误差评分 " + errorScoreValue.Text + (result.Score.HasValue ? " / 100（越高误差越小）" : "") + "；容差 " + (result.Tolerance.HasValue ? Metric(result.Tolerance.Value) + " ct" : "未设置") + "；" + analysisCoverage.Text +
                "\r\n按有效时长加权、线性插值；评分仅供相同容差下比较。\r\n评分公式：" + DiagnosticErrorAnalysis.ScoreFormula;
        }
    }
}
