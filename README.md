# 猫咪数织（Nonogram）

Unity 手机数织游戏原型。玩家根据行列数字填出图案；关卡支持多种画笔颜色与形状，也支持用图案模式查看线索。

## 打开工程

1. 使用 **Unity 6000.0.34f1** 打开项目根目录。
2. 确认 Feel / More Mountains 插件已导入：游戏的音效反馈和 `MMSoundManager` 使用了它。当前 `.gitignore` 排除了 `Assets/Feel`，新克隆的工程需要自行补齐该依赖。
3. 打开 `Assets/Scenes/SampleScene.unity`，进入 Play 模式体验游戏。
4. 打开 `Assets/Scenes/LevelEditor.unity`，进入 Play 模式制作关卡。当前 Build Settings 只包含 `SampleScene`；如果要单独构建编辑器场景，需要先将它加入 Build Settings。

项目使用 UGUI 和 Universal Render Pipeline；Unity Package Manager 依赖记录在 `Packages/manifest.json`。

## 制作关卡

在关卡编辑器中设置宽、高（5–20），点击“新建关卡”，然后选择画笔并点击格子绘制。可增加画笔、设置颜色和形状、切换填充或标记模式，并选择普通、困难或超难。

- **加载关卡**：从 `Assets/StreamingAssets/Levels` 选择已有 JSON，修改后点“保存关卡”会写回原文件。未保存的编辑在切换关卡前会提示。
- **调整当前尺寸**：修改滑动条后点击该按钮；保留左上角重叠区域，新增格子为空，超出范围的格子会被裁剪，试玩统计会重置。
- **检查唯一解**：根据当前行列线索检查解的数量；复杂关卡可能返回“暂未能确认”。
- **试玩当前关卡**：使用当前编辑内容开局。通关后记录试玩次数、最近、最快和平均通关时间；点击“保存关卡”后这些统计会写入 JSON。
- **保存关卡**：新关卡命名为 `Nonogram_yyyyMMdd_HHmmss_fff.json`，保存在 `Assets/StreamingAssets/Levels`。

编辑器能看到目录中的 JSON，不代表它已经进入游戏关卡顺序。要把新关卡加入游戏，请在 `Assets/Data/NonogramLevelListData.asset` 的 `levelFiles` 中填写文件名；数组第一个元素是第 1 关。当前该列表只配置了 `Nonogram_1.json`。

## 游戏规则与数据

每行、每列的数字表示连续填充段。玩家有 5 次尝试机会和 3 次提示机会；正确填充所有目标格即通关，不需要标记所有空格。正确填格、完成行列和剩余机会会计分。设置中可切换音乐、音效、图案模式和调试模式；调试模式会显示关卡列表按钮。

关卡使用 JSON。`width`、`height` 指定棋盘尺寸；`cells` 按从左到右、从上到下排列，每格的 `state` 为 `Blank(0)`、`Filled(1)` 或 `Marked(2)`，填充格用 `brushId` 指向 `brushes`。画笔包含 `colorHex` 和 `shape`；`clues` 由格子重新计算。编辑器试玩统计保存在关卡的 `playtest` 字段，玩家尝试次数、通关时间、最高分和星级则单独保存在设备的 `PlayerPrefs` 中。

主要代码位于 `Assets/_Scripts/Nonogram/`；关卡编辑器入口是 `Assets/_Scripts/LevelEditorRuntime.cs`。
