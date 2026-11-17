# AFrame

AFrame 是面向小型单机游戏的模块化 Unity 6.6 框架。纯 C# 模块在固定 Tick 中处理命令和输入，Unity 负责项目装配、场景和表现。视图用 Query 读取状态，用 Event 接收变化通知。App 服务跨会话保留，重开只创建一局新的 Session。

v2.2 首版的五项补齐已验收，覆盖 EditMode 测试、架构和定义校验、Editor 运行与关闭时的任务检查，以及 macOS arm64 Mono Player 的 Character 双视图切换。详见 [首版验收清单](Docs/AFrame-v2.2-Acceptance.md)。

## 接入已有 Unity 项目

通过 Git 克隆本仓库后，运行 `git lfs pull` 获取 2D 示例使用的精灵资源。

关闭目标项目的 Unity 编辑器，在 AFrame 仓库根目录运行。需要 Python 3。

```sh
python3 Tools/install-aframe "/你的 Unity 项目路径"
```

脚本会安装五个基础包并复制命令行工具。打开项目后，选择 **AFrame → Initialize Project**，再打开生成的 `Assets/Game/Scenes/AFrameStart.unity` 进入 Play Mode。Bootstrap 自动读取默认 Profile，启动带有 `SceneScope` 的场景。

想直接试玩能源回收站，在首次安装时加上 `--sample`，并在 Unity 打开 EnergyStation 场景。安装示例后，在 **Player → Active Input Handling** 选择 **Both**。示例使用 Input System 读取移动输入，桌面 Player 的菜单按钮使用 IMGUI。示例需要空的 `Assets/Game` 路径，并随安装带入 Input System 和 Save 包。也可以直接在本仓库运行示例。

## 开发玩法

1. 用 **AFrame → Generate → Feature** 生成功能骨架。规则放在 `Core`，跨功能公开接口放在 `Contracts`，视图和输入放在 `Unity`。
2. 通过生成的模块资产在 `Composition` 装配玩法，将资产加入默认 Profile，再把视图挂到场景的 `SceneScope` 下。需要独立菜单或按场景装配模块时，实现 `AFrameSceneEntry`。
3. 玩法配置可放在功能的 `Definitions/*.aframe.json`。导入后通过 App 服务提供强类型定义。本示例的角色速度由 [Character 定义](Assets/Game/Features/Character/Definitions/character.default.aframe.json)驱动。
4. 修改功能后执行 **AFrame → Project → Build Context**，再校验架构。Git 中的 `Context/` 可供 AI 读取程序集依赖和公共 API，运行时无需 AI 服务。

```sh
Tools/aframe task start <id>
Tools/aframe import
Tools/aframe context
Tools/aframe test --feature character
Tools/aframe check --task <id>
Tools/aframe smoke --scene Assets/Game/Scenes/EnergyStation.unity
```

`check` 基于任务开始时的工作区，只检查本次改动，再运行声明的自动检查。`acceptance` 可写 EditMode 测试全名或通配符，也可用 `test:` 显式指定测试选择器。自然语言验收项会在报告中列出，供手工确认。CLI 可以启动批处理，也可以通过本地文件向当前 Editor 发出请求。报告在 `Logs/AFrame/`。

## Character 的 2D／3D 示例

打开 [Character2D.unity](Assets/Game/Scenes/Character2D.unity) 或 [AFrameSampleScene.unity](Assets/Game/Scenes/AFrameSampleScene.unity)，进入 Play Mode 后可用方向键或 WASD 移动，也可按 **Tab** 或用界面按钮切换场景。两个场景共用 Character 的命令、规则和输入。2D 将领域平面映射到 XY，3D 映射到 XZ。

[首版验收清单](Docs/AFrame-v2.2-Acceptance.md)说明常用验证命令和验收状态。

## 能源回收站

打开 [EnergyStation.unity](Assets/Game/Scenes/EnergyStation.unity)，进入 Play Mode 后点击 **开始**。用 **WASD 或方向键**移动，按住 **Shift** 冲刺，在 **45 秒内**收集三个核心并进入出口。**Esc** 暂停或继续，**R** 重开。

按 **F5** 或点 **保存** 可以手动保存一局。退出后重新打开游戏，在主菜单按 **L** 或点 **读取存档**。存档位于 `Application.persistentDataPath/AFrame/energy-station.json`，包含角色位置、核心、剩余时间和关卡状态。

当前目标为桌面单人离线游戏。程序集、模块作用域、持久化和验证范围见 [v2.2 设计与迁移记录](Docs/AFrame-Architecture-v2.2.md)。
