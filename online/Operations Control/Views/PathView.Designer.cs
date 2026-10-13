using System.Drawing;
using System.Windows.Forms;
using Operations_Control.Controls;

namespace Operations_Control.Views
{
    partial class PathView
    {
        private System.ComponentModel.IContainer components;
        private TableLayoutPanel root, previewLayout;
        private FlowLayoutPanel actions, planning, geometry, deviceActions, advanced, transforms;
        private TabControl tabs;
        private TabPage drawingTab, programTab, analysisTab, deviceTab;
        private Label feedbackLabel, deviceLabel;
        private Button importButton, clearButton, clearActualButton, undoButton, shapeButton, transformButton, planButton, runButton, saveProgramButton, analyzeButton, exportButton;
        private Button enableButton, disableButton, homeButton;
        private ComboBox shape, interpolation, homeTarget;
        private NumericUpDown centerX, centerY, radius, endX, endY, startAngle, sweepAngle, scale, offsetX, offsetY, speed, acceleration, spacing, coordinate, programNumber, lag, plcNumber, homeTimeout, unitlessScale;
        private CheckBox hardwareConfirmed;
        private MotionCanvas drawingCanvas, actualCanvas;
        private TextBox programText, analysisText;
        private Timer pollTimer;
        private TableLayoutPanel settingsLayout;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }
        private static NumericUpDown Number(decimal min, decimal max, decimal value, int decimals)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, Value = value, DecimalPlaces = decimals, Increment = decimals == 0 ? 1 : 0.1M, Width = 82, Margin = new Padding(3) };
        }
        private static void Field(FlowLayoutPanel panel, string caption, Control control)
        {
            Label label = new Label { Text = caption, AutoSize = true, Location = new Point(0, 8) };
            int width = TextRenderer.MeasureText(caption, new Font("Microsoft YaHei UI", 10)).Width + 4;
            Panel pair = new Panel { Width = width + control.Width + 8, Height = 34, Margin = new Padding(3, 2, 4, 2) };
            control.Location = new Point(width + 4, 4); pair.Controls.Add(label); pair.Controls.Add(control); panel.Controls.Add(pair);
        }
        private static Button ActionButton(string text)
        { return new Button { Text = text, AutoSize = true, Height = 32, Margin = new Padding(3), Padding = new Padding(5, 1, 5, 1) }; }
        private static GroupBox Plot(string title, Control canvas)
        {
            GroupBox group = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(8), Margin = new Padding(3), Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold) };
            canvas.Font = new Font("Microsoft YaHei UI", 9); group.Controls.Add(canvas); return group;
        }
        private void UpdateShapeFields()
        {
            radius.Parent.Visible = shape.SelectedIndex != 0;
            endX.Parent.Visible = endY.Parent.Visible = shape.SelectedIndex == 0;
            startAngle.Parent.Visible = sweepAngle.Parent.Visible = shape.SelectedIndex == 2;
            ResizeGeometry();
        }
        private void ResizeGeometry()
        {
            if (geometry.Parent == null) return;
            geometry.PerformLayout();
            int height = 44;
            foreach (Control item in geometry.Controls)
                if (item.Visible) height = System.Math.Max(height, item.Bottom + item.Margin.Bottom + 4);
            ((TableLayoutPanel)geometry.Parent).RowStyles[0].Height = height;
        }
        public void AddCoordinateSettings(CanvasView canvases)
        {
            canvases.ShowSettingsOnly(); settingsLayout.Controls.Add(canvases, 0, 0);
        }
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            root = new TableLayoutPanel(); actions = new FlowLayoutPanel(); planning = new FlowLayoutPanel(); geometry = new FlowLayoutPanel();
            tabs = new TabControl(); drawingTab = new TabPage("绘图与运行"); programTab = new TabPage("程序"); analysisTab = new TabPage("分析"); deviceTab = new TabPage("设置");
            feedbackLabel = new Label { Visible = false }; deviceLabel = new Label(); previewLayout = new TableLayoutPanel();
            importButton = ActionButton("导入 DXF"); clearButton = ActionButton("清空绘图"); undoButton = ActionButton("撤销"); shapeButton = ActionButton("添加"); transformButton = ActionButton("应用变换");
            clearActualButton = ActionButton("清空轨迹");
            planButton = ActionButton("生成规划"); runButton = ActionButton("运行"); saveProgramButton = ActionButton("保存程序"); analyzeButton = ActionButton("分析"); exportButton = ActionButton("导出数据");
            enableButton = ActionButton("使能"); disableButton = ActionButton("失能"); homeButton = ActionButton("机械回零");
            shape = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 }; shape.Items.AddRange(new object[] { "直线", "圆", "圆弧", "五角星" }); shape.SelectedIndex = 1;
            interpolation = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 }; interpolation.Items.AddRange(new object[] { "PVT", "Spline1" }); interpolation.SelectedIndex = 0;
            homeTarget = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 }; homeTarget.Items.AddRange(new object[] { "X 轴", "Y 轴", "X/Y 两轴" }); homeTarget.SelectedIndex = 2;
            centerX = Number(-1000000, 1000000, 0, 3); centerY = Number(-1000000, 1000000, 0, 3); radius = Number(0.001M, 1000000, 5, 3);
            endX = Number(-1000000, 1000000, 5, 3); endY = Number(-1000000, 1000000, 5, 3); startAngle = Number(-360, 360, 0, 1); sweepAngle = Number(-360, 360, 90, 1);
            scale = Number(0.0001M, 10000, 1, 4); offsetX = Number(-1000000, 1000000, 0, 3); offsetY = Number(-1000000, 1000000, 0, 3); unitlessScale = Number(0.001M, 1000000, 1, 3);
            speed = Number(0.001M, 1000, 5, 3); acceleration = Number(0.001M, 100000, 20, 3); spacing = Number(0.001M, 100, 0.5M, 3);
            coordinate = Number(1, 16, 1, 0); programNumber = Number(1, 32767, 20, 0); lag = Number(0, 2000, 80, 0); plcNumber = Number(-1, 31, -1, 0); homeTimeout = Number(1, 600, 30, 0);
            hardwareConfirmed = new CheckBox { Text = "实机配置已确认", AutoSize = true, Margin = new Padding(6, 6, 3, 3) };
            if (drawingCanvas == null) drawingCanvas = new MotionCanvas { Dock = DockStyle.Fill, DrawingEnabled = true, PathColor = Color.FromArgb(37, 140, 114) };
            if (actualCanvas == null) actualCanvas = new MotionCanvas { Dock = DockStyle.Fill, PathColor = Color.FromArgb(39, 91, 204) };
            drawingCanvas.Dock = actualCanvas.Dock = DockStyle.Fill;
            programText = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 11) };
            analysisText = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Font = new Font("Microsoft YaHei UI", 10) };
            pollTimer = new Timer(components); pollTimer.Interval = 50; pollTimer.Tick += Poll;
            SuspendLayout(); Font = new Font("Microsoft YaHei UI", 10);
            root.Dock = DockStyle.Fill; root.ColumnCount = 1; root.RowCount = 3;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            actions.Dock = DockStyle.Fill; actions.WrapContents = false;
            actions.Controls.AddRange(new Control[] { importButton, clearButton, clearActualButton, undoButton, planButton, runButton });
            planning.Dock = DockStyle.Fill; planning.WrapContents = false;
            Field(planning, "插补", interpolation); Field(planning, "速度 mm/s", speed); Field(planning, "加速度 mm/s²", acceleration); Field(planning, "间距 mm", spacing);
            tabs.Dock = DockStyle.Fill; tabs.TabPages.AddRange(new TabPage[] { drawingTab, programTab, analysisTab, deviceTab });
            TableLayoutPanel drawingLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            drawingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            drawingLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); drawingLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            geometry.Dock = DockStyle.Fill;
            Field(geometry, "图形", shape); Field(geometry, "X mm", centerX); Field(geometry, "Y mm", centerY); Field(geometry, "半径 mm", radius);
            Field(geometry, "终点 X", endX); Field(geometry, "Y", endY); Field(geometry, "起角°", startAngle); Field(geometry, "扫角°", sweepAngle); geometry.Controls.Add(shapeButton);
            shape.SelectedIndexChanged += delegate { UpdateShapeFields(); }; UpdateShapeFields();
            previewLayout.Dock = DockStyle.Fill; previewLayout.ColumnCount = 2; previewLayout.RowCount = 1;
            previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            previewLayout.Controls.Add(Plot("实际轨迹", actualCanvas), 0, 0); previewLayout.Controls.Add(Plot("绘图", drawingCanvas), 1, 0);
            drawingLayout.Controls.Add(geometry, 0, 0); drawingLayout.Controls.Add(previewLayout, 0, 1); drawingTab.Controls.Add(drawingLayout);
            geometry.SizeChanged += delegate { ResizeGeometry(); };
            geometry.ControlAdded += delegate { ResizeGeometry(); };
            TableLayoutPanel programLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            programLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            programLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); programLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            FlowLayoutPanel programActions = new FlowLayoutPanel { Dock = DockStyle.Fill }; programActions.Controls.Add(saveProgramButton);
            programLayout.Controls.Add(programActions, 0, 0); programLayout.Controls.Add(programText, 0, 1); programTab.Controls.Add(programLayout);
            TableLayoutPanel analysisLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            analysisLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            analysisLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); analysisLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            FlowLayoutPanel analysisActions = new FlowLayoutPanel { Dock = DockStyle.Fill }; analysisActions.Controls.Add(analyzeButton); analysisActions.Controls.Add(exportButton);
            analysisLayout.Controls.Add(analysisActions, 0, 0); analysisLayout.Controls.Add(analysisText, 0, 1); analysisTab.Controls.Add(analysisLayout);
            settingsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(6) };
            settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new int[] { 54, 44, 34, 84, 48, 44 }) settingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            settingsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            advanced = new FlowLayoutPanel { Dock = DockStyle.Fill };
            Field(advanced, "坐标系", coordinate); Field(advanced, "程序号", programNumber); Field(advanced, "DXF 比例", unitlessScale); Field(advanced, "模拟滞后 ms", lag);
            settingsLayout.Controls.Add(advanced, 0, 1); settingsLayout.Controls.Add(hardwareConfirmed, 0, 2);
            deviceLabel.Dock = DockStyle.Fill; deviceLabel.Text = "未连接"; settingsLayout.Controls.Add(deviceLabel, 0, 3);
            deviceActions = new FlowLayoutPanel { Dock = DockStyle.Fill };
            deviceActions.Controls.Add(enableButton); deviceActions.Controls.Add(disableButton); Field(deviceActions, "回零轴", homeTarget); Field(deviceActions, "PLC", plcNumber); Field(deviceActions, "超时 s", homeTimeout); deviceActions.Controls.Add(homeButton);
            settingsLayout.Controls.Add(deviceActions, 0, 4);
            transforms = new FlowLayoutPanel { Dock = DockStyle.Fill };
            Field(transforms, "缩放", scale); Field(transforms, "偏移 X mm", offsetX); Field(transforms, "Y mm", offsetY); transforms.Controls.Add(transformButton); settingsLayout.Controls.Add(transforms, 0, 5);
            deviceTab.Controls.Add(settingsLayout);
            root.Controls.Add(actions, 0, 0); root.Controls.Add(planning, 0, 1); root.Controls.Add(tabs, 0, 2); Controls.Add(root);
            AutoScaleMode = AutoScaleMode.Dpi; Size = new Size(1160, 680); Margin = new Padding(0);
            ResumeLayout(false);
        }
    }
}
