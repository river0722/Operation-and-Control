# 认清熬夜差距 · 汕头大学上位机背景

交付两种配色，保留完整对比度，没有提前降低不透明度：

- `background-black-red-1920x1080.png`：黑红配色，暖白校徽。
- `background-purple-gold-1920x1080.png`：紫金配色，金色校徽。

两张成品均为 1920×1080 PNG，可在软件中按实际界面调节背景不透明度。主体和队名字标位于右侧，左侧与中央保留操作区域。

背景由 codex-image2 技能生成，模型 `gpt-image-2`，质量 `high`，请求尺寸为 1920×1080。服务实际返回 1672×941，交付时经过高质量插值导出为 1920×1080。完整提示词保存在 `prompt-black-red.txt`、`prompt-purple-gold.txt` 和 `jobs.jsonl` 中。

校徽使用汕头大学官方素材，生成背景后再合成，保留原有图形轮廓。源文件及单色应用说明：

- 官方标识说明：https://www.stu.edu.cn/xxgk/bs.htm
- 官网透明标志：https://www.stu.edu.cn/images/logo.png
- 官方标识规范图：https://www.stu.edu.cn/__local/7/6A/B3/E68186E04CC92E989926EC84B24_43E7CFB8_36613.jpg

`stu-official-crest.png` 为官网透明标志左侧校徽的裁切；交付中根据背景采用暖白和金色单色版本。`compose-official-crest.ps1` 记录合成位置、颜色和尺寸。
