# DesktopFolder · 桌面挂靠文件夹小组件

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://www.microsoft.com/windows)

一个悬浮在桌面的文件夹小组件(WPF / C# / .NET 10):毛玻璃质感、多窗口持久化、边缘缩放带容量上限、图标铺满网格,一键访问常用文件夹。

A desktop-attached folder widget (WPF / C# / .NET 10): frosted-glass look, multi-window persistence, edge resizing with a capacity limit, icon-filled grids — one-click access to your frequently used folders.

---

## ✨ 功能特性 / Features

### 中文

- **桌面挂靠** — 窗口附着在桌面层(WorkerW),永远在普通窗口之下,不打扰工作区;也可作为普通窗口使用
- **毛玻璃质感** — 半透明背景 + 系统级模糊(SetWindowCompositionAttribute),A 状态缩略图带磨砂边缘
- **双态界面** — A 折叠缩略图(小网格)/ B 展开面板(可滚动大网格,自动铺满)
- **容量限制缩放** — 拖动窗口边缘自由缩放;当网格已能容纳文件夹内**全部**图标时,自动禁止继续拉伸,图标"尽可能"排满界面
- **多窗口持久化** — 可创建任意多个小组件,位置/大小/网格模式/图标排列全部保存在 `config.json`,重启自动还原
- **丰富的交互** — 点击缩略图打开文件/文件夹;拖动边缘缩放;拖动窗口空白处移动;双击抑制防误开
- **图标整理** — 展开面板中可自由拖动图标排列(位置持久化),也可拖出到桌面移动/复制文件;支持 .url / .website 快捷方式,自动隐藏扩展名
- **右键菜单** — 更换目录 / 刷新 / 新建小组件 / 删除小组件 / 隐藏扩展名 / 网格模式(2×/3×)/ 开机自启 / 退出
- **轻量** — 无边框、无任务栏图标,仅一个 exe

### English

- **Desktop-attached** — The widget lives on the desktop layer (WorkerW), always below normal windows, never in your way
- **Frosted glass** — Translucent background with system-level blur (`SetWindowCompositionAttribute`); the collapsed thumbnail view has frosted edges
- **Two-state UI** — Collapsed thumbnail grid (A state) / expanded panel (B state, scrollable, auto-fills)
- **Capacity-limited resizing** — Drag the edges to resize freely; once the grid fits **all** icons in the folder, further stretching is automatically blocked so the icons fill the surface "as much as possible"
- **Multi-window persistence** — Create any number of widgets; positions, sizes, grid modes and icon layouts are saved in `config.json` and restored on launch
- **Rich interactions** — Click a thumbnail to open; drag edges to resize; drag the empty area to move; double-click suppression prevents accidental opens
- **Icon management** — Rearrange icons freely in the expanded panel (positions persist), or drag them out to the desktop to move/copy files; `.url` / `.website` shortcuts supported, extensions auto-hidden
- **Context menu** — Change folder / refresh / new widget / delete widget / hide extensions / grid mode (2× / 3×) / auto-start on boot / exit
- **Lightweight** — Borderless, no taskbar icon, single exe

---

## 📖 使用 / Usage

### 中文

1. 运行 `DesktopFolder.exe`(或从 Release 下载)
2. 右键小组件 →「更换目录」选择要展示的文件夹
3. 交互方式:
   - **打开**:点击 A 状态中的缩略图
   - **展开/收起**:点击缩略图网格外的空白处;或点击右上角齿轮菜单
   - **移动**:按住 A 状态空白区域拖动
   - **缩放**:拖动窗口边缘/四角(受容量上限约束)
   - **排列图标**:在 B 面板中直接拖动图标
   - **快速置入**:从资源管理器把文件拖进小组件窗口
4. 右键菜单可新建/删除小组件、切换 2×/3× 网格、开启开机自启

### English

1. Run `DesktopFolder.exe` (or grab it from Releases)
2. Right-click the widget → "Change folder" (更换目录) to pick a folder
3. Interactions:
   - **Open**: click a thumbnail in the A state
   - **Expand / collapse**: click the empty area outside the thumbnail grid (or the gear menu)
   - **Move**: drag the empty area of the widget
   - **Resize**: drag an edge or corner (subject to the capacity limit)
   - **Arrange icons**: drag icons inside the expanded panel
   - **Drop in**: drag files from Explorer onto the widget
4. Use the context menu to create/delete widgets, switch 2×/3× grids, or enable auto-start

---

## 🛠 构建 / Build

### 中文

```powershell
dotnet build DesktopFolder.csproj -c Release
# 产物: bin/Release/net10.0-windows/DesktopFolder.exe
```

要求:.NET 10 SDK + Windows 10/11。

### English

```powershell
dotnet build DesktopFolder.csproj -c Release
# Output: bin/Release/net10.0-windows/DesktopFolder.exe
```

Requires: .NET 10 SDK + Windows 10/11.

---

## ⚙️ 配置 / Configuration

### 中文

配置文件 `config.json` 与 exe 同目录,运行后自动生成。每个小组件包含:

| 字段 | 说明 |
| --- | --- |
| `folderPath` | 展示的文件夹路径 |
| `x` / `y` / `width` / `height` | 窗口位置与大小 |
| `gridMode` | 网格模式(2 = 2×, 3 = 3×) |
| `hideExtensions` | 是否隐藏扩展名 |
| `positions` | 展开面板中的图标排列(按路径保存) |

### English

`config.json` sits next to the exe and is auto-generated on first run. Each widget entry:

| Field | Description |
| --- | --- |
| `folderPath` | The folder to display |
| `x` / `y` / `width` / `height` | Window position and size |
| `gridMode` | Grid mode (2 = 2×, 3 = 3×) |
| `hideExtensions` | Hide file extensions |
| `positions` | Icon layout in the expanded panel (keyed by path) |

---

## 🧪 技术实现 / Implementation Notes

- WPF 透明分层窗口 + `SetWindowCompositionAttribute` 系统模糊
- `WorkerW` 桌面层挂靠(枚举桌面窗口找到挂靠宿主)
- 命中测试缩放带:毛玻璃边缘 + 内部 6px,四角弧形对角线判定,底部包含名称行
- 容量限制算法:列数 × 行数(42px/格)与文件夹条目数比较,达到上限即锁定增长方向
- 图标缓存 `ConcurrentDictionary` + 退出清理,减少重复取图标开销

---

## 📄 协议 / License

[MIT License](LICENSE) — Copyright (c) 2026 BinaryBOOMErCattttt
