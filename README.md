# 桌面天气 · Desktop Weather

为 Windows 桌面添加四季粒子特效。樱花、萤火虫、夏雨、落叶和雪花显示在浏览器、VS Code 等普通应用窗口上方，鼠标可以继续操作下方窗口。

当前版本：**1.3.1**。使用 **C#、.NET 10、WPF 和 Win32 透明窗口**，没有额外第三方绘图依赖。

![桌面天气动态效果展示](docs/demo.gif)

## 功能

- 春日樱花：粉白花瓣随风飘落并旋转。
- 夏夜萤火：黄绿色光点缓慢漂浮，独立明暗闪烁。
- 夏日细雨：淡蓝色雨丝快速下落，风力控制倾斜与偏移。
- 秋日落叶：暖色叶片随风运动。
- 冬日雪花：近景较大且虚焦，远景较小且清晰；中远景中尺寸足够显示细节的雪花约有 30% 为六角形。
- 六套预设：春樱、夏夜、夏雨、秋风、轻雪、大雪。
- 实时调整数量、大小、运动速度、风力和不透明度。
- 多屏显示、鼠标穿透、托盘控制、暂停与停止、设置自动保存。
- **Ctrl + Alt + F12** 随时停止特效。

## 构建与运行

需要 Windows 10/11 x64 和 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。

```powershell
git clone https://github.com/twlSEU/DesktopWeather.git
cd DesktopWeather

# 编译 Debug 版本
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1

# 打开软件
powershell -NoProfile -ExecutionPolicy Bypass -File .\Run.ps1

# 发布自带运行环境的单文件 EXE
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1 -Publish
```

发布程序位于 `release\DesktopWeather.exe`，无需另装 .NET 运行时。首次打开显示设置面板和预览，点击右下角开启按钮后才播放桌面特效。

构建脚本优先使用项目目录中的 `.tools\dotnet\dotnet.exe`，没有本地 SDK 时使用 PATH 中的 `dotnet`。SDK 和编译产物不存入 Git 仓库。

可以使用 VS Code 和 Microsoft C# 扩展开发，按 **Ctrl + Shift + B** 构建。使用项目的调试配置时，将 `.vscode/launch.json` 的 `DOTNET_ROOT` 按本机 SDK 位置设置；SDK 位于 `.tools/dotnet` 时已有配置可直接使用。

## 验证

```powershell
# 动画、设置与本软件界面/窗口检查
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1 -Test

# 检查发布版本
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1 -Publish -Test

# 测量当前显示器上 700 颗雪花的性能
.\release\DesktopWeather.exe --benchmark .\benchmark-results smooth snow
```

`--benchmark` 支持 `snow`、`leaves`、`petals`、`fireflies`、`rain`。先预热 2 秒，再测量 5 秒，结果写入指定目录。FPS 指本软件的动画更新/图层提交速率。

1.3.1 本机验证通过 32 项动画与设置检查、60 项界面与窗口检查。在 3840×2160 屏幕、700 颗雪花、大小 7、速度 100、风力 25、不透明度 90、关闭节能限制时，实测约 58.5 FPS。实际性能取决于设备、分辨率和参数。

## 绘制方式

设置面板和预览使用 WPF；全屏特效使用 Win32 分层透明窗口、独立绘制线程和预乘 BGRA 像素缓冲区。粒子纹理和雪花虚焦纹理预先生成并缓存，动画中复用；白色雪花使用专门的透明度混合。播放桌面特效时停止面板内的重复预览。

## 项目结构

| 路径 | 内容 |
| --- | --- |
| `MainWindow.xaml`、`MainWindow.xaml.cs` | 设置与预览界面 |
| `Models/` | 设置、模式与预设 |
| `Rendering/` | 粒子模拟、景深纹理、绘制与帧率统计 |
| `Interop/`、`OverlayWindow.cs` | Win32 窗口、鼠标穿透与屏幕覆盖 |
| `Services/` | 屏幕管理、托盘、快捷键与设置存储 |
| `tests/`、`SelfTestRunner.cs` | 动画、设置与界面检查 |

更多操作说明见 [使用说明.txt](使用说明.txt)。

## 设置与范围

个人设置写入 EXE 同目录的 `settings.json`，该文件不进入 Git 仓库。关闭面板默认保留托盘图标；「退出软件」会退出并清除特效层。

支持普通桌面应用窗口；Windows 安全桌面、锁屏和独占全屏游戏不保证显示。屏幕连接或分辨率变化会重建特效层，不同设备的多屏与 DPI 缩放仍需实际验证。
