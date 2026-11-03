<div align="right">
  <b>🇨🇳 中文</b> | <a href="#-windowsticky---window-pinned-sticky-notes">🇬🇧 English</a>
</div>

<br />

# 📌 WindowSticky - 窗口贴身便签

> 一款专为“边看边写”设计的 Windows 桌面便签工具。不再是死板地贴在桌面上，而是**紧紧钉在你当前的工作窗口旁**。

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)

## 💡 为什么做这个？

传统的桌面便签，要么永远置顶挡住视线，要么被其他窗口压在底下找不到。
在查阅资料、写代码、或者看视频时，我们经常需要**顺手记点东西**。
`WindowSticky` 解决的就是这个问题：**你可以把便签“钉”在任何窗口上，它会成为那个窗口的卫星，随它移动，随它缩放。**

## ✨ 核心特性

- 📌 **无感贴身跟随**：点击图钉，选择任意窗口，便签即刻吸附。目标窗口移动，它跟着走；目标窗口最小化，它自动隐藏；目标关闭，它自动解绑。
- 🎨 **极简滑动操作**：主界面采用流畅的滑动交互设计。鼠标按住卡片向左拖动，即可拉出“复制”与“删除”按钮，告别突兀的常驻按钮。
- 🌈 **5 种莫兰迪预设色**：自带 5 款温和不刺眼的主题色，支持一键切换，主界面卡片与便签本体颜色同步。
- 🚀 **轻量且即插即用**：单文件绿色版，无需安装，不写注册表（仅读取本地 JSON 记录），随拷随用。
- 💾 **自动持久化**：采用 600ms 防抖自动保存，任何灵感都不会丢失。

## 📸 界面预览

<!-- 建议在这里放一张你用 ScreenToGif 录制的演示动图或图片 -->
<p align="center">
  <i>（演示动图制作中，欢迎下载体验）</i>
</p>

## 🚀 如何下载使用

1. 前往本仓库右侧的 **[Releases](../../releases)** 页面。
2. 在最新版本中下载 `WindowStickyNote.exe`。
3. 双击即可直接运行（无需安装 .NET 运行库）。

> ⚠️ **安全提示**：由于本项目为个人开源项目，暂无商业代码签名证书。首次运行时，如果 Windows Defender 弹出“Windows 已保护你的电脑”的蓝色拦截弹窗，请点击 **“更多信息” -> “仍要运行”**。本项目完全开源，代码透明，请放心使用。

## 🛠️ 快捷键与操作指南

- **新建便签**：在主界面点击右上角 `＋ 新建`。
- **钉选窗口**：在便签窗口点击顶栏的 `📌` 图标，然后点击你想要绑定的目标窗口即可。
- **移动位置**：按住便签的顶栏，拖拽到目标窗口的任意位置松手，它会记住这个相对位置。
- **解除绑定**：再次点击便签顶栏的 `📍` 图标。

## 💻 本地开发

如果你想自己编译或参与开发，请确保安装了 [.NET 10 SDK](https://dotnet.microsoft.com/)：

```bash
# 克隆仓库
git clone https://github.com/j1nx1u/WindowStickyNote.git

# 运行项目
dotnet run

# 打包为单文件绿色版
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false