# AFrame 通用模块化 Unity 框架 v2.1

## 定位

AFrame 面向中小型 Unity 项目，采用“核心运行时、可选适配器、项目功能”的结构。2D、3D、单机和联机通过适配器与配置组合，玩法规则保持在 Unity 无关的 C# 程序集中。

v2.1 是可实施版本。它冻结首版必须遵守的边界，推迟网络预测、回滚、完整代码生成和复杂场景自动化，避免框架本身超过项目需求。

这份文档是目标架构。当前仓库仍有部分 v1 实现，迁移状态见“当前实现与迁移”。

## 首版目标

首版只交付以下能力：

1. Foundation、Runtime、Data、Unity、Editor 的程序集边界。
2. Session 级模块图、命令队列、固定 Tick、事件队列和查询接口。
3. 一个 Character 或 Inventory 垂直功能示例。
4. 文本定义导入、稳定 ID、版本字段和源文件哈希。
5. 架构验证、上下文生成、任务范围检查和 Play Mode Smoke Check。

Network、Physics 2D/3D、UI、Audio、Camera、快照代码生成和 CLI 命令通道先保留接口，按后续阶段逐项实现。

## 包与程序集

包是分发边界，程序集是架构边界。首版可以把核心程序集放进一个 `com.aframe.core` 分发包，但不能合并程序集：

```text
Packages/
  com.aframe.core/
    Runtime/Foundation/AFrame.Foundation.asmdef
    Runtime/Runtime/AFrame.Runtime.asmdef
    Runtime/Data/AFrame.Data.asmdef
  com.aframe.unity/
  com.aframe.editor/
  com.aframe.modules/          # 稳定后再启用；外部依赖较重的模块可单独分包
```

依赖方向固定为：

```text
AFrame.Foundation
        ↓
AFrame.Runtime      AFrame.Data
        ↓              ↓
                AFrame.Unity
                       ↓
                 Game Features
```

`Foundation`、`Runtime` 和功能 Core 程序集设置 `noEngineReferences: true`。Input System、Addressables、Unity Transport 等可选依赖不得进入核心程序集。

## 功能边界

每个功能使用三个程序集。功能之间只引用对方的 Contracts：

```text
Assets/Game/Features/Inventory/
  Contracts/Inventory.Contracts.asmdef
    Commands/
    Events/
    Queries/
    Definitions/
  Core/Inventory.Core.asmdef
    Domain/
    Application/
  Unity/Inventory.Unity.asmdef
    Views/
    Adapters/
  Definitions/
  Generated/             # 工具生成，禁止手改
  feature.json
  README.md
```

`Contracts` 是功能对外的唯一 public API。Domain 和 Application 默认使用 `internal`。功能依赖以 asmdef 为准，`feature.json` 只记录无法从代码和目录推导的元数据，例如版本、简介、物理职责和支持的运行模式。

## 运行时模型

所有写操作经过命令，所有读操作经过查询，状态变化通过事件通知：

```text
Input / Network
      ↓
CommandEnvelope
      ↓
CommandQueue
      ↓
Handler
      ↓
Domain state
      ↓
DomainEventQueue
      ↓
View / UI / Save / Network
```

首版使用固定步长 Tick，默认 50Hz。Unity 的 `Update` 只采样输入和推进累加器，领域逻辑不读取 Unity 时间。

每个 Tick 的顺序固定为：

1. Receive：接收本地或远端命令并分配 Tick、Issuer、Sequence。
2. Dispatch：按稳定顺序执行本 Tick 命令；失败产生带错误码的 `CommandRejected`。
3. Simulate：按模块顺序运行 `IFixedTickable`。
4. Flush：统一分发事件；事件期间产生的命令进入下一个 Tick。
5. Snapshot：首版只保留扩展点，存档和联机快照在后续阶段实现。

必须配置单帧最大补 Tick 数、超时处理、暂停行为和迟到命令策略。事件订阅者只能更新表现或提交命令，不能直接修改其他功能的领域状态。

## 核心接口方向

首版冻结以下概念：

```csharp
public interface IGameModule
{
    ModuleId Id { get; }
    ModuleScope Scope { get; }
    void Configure(IModuleBuilder builder);
    void Start(ModuleContext context);
    void Stop();
}

public interface ICommand { }
public interface ICommandHandler<TCommand> where TCommand : struct, ICommand
{
    Result Handle(in CommandEnvelope<TCommand> command);
}

public interface IDomainEvent { }
public interface IFixedTickable
{
    void FixedTick(in TickContext context);
}

public interface ICommandTransport
{
    void Send<TCommand>(CommandEnvelope<TCommand> command)
        where TCommand : struct, ICommand;
}
```

命令类型、事件类型和定义 ID 必须有稳定的生成 ID。`IServiceResolver` 只允许出现在模块注册工厂和组合根，领域对象通过构造函数接收依赖。服务需要声明作用域、提供者数量和生命周期。

首版保持同步 `Start`。异步资源加载放在 Unity 适配器中，待作用域和取消语义稳定后再引入异步模块生命周期。

## 作用域与组合根

首版实现 App 和 Session 两级作用域，Scene 作用域作为 Unity 适配器扩展：

| 作用域 | 示例 | 规则 |
| --- | --- | --- |
| App | 日志、定义注册表、设置 | 不读取 Session 服务 |
| Session | 角色、背包、任务 | 随一局游戏创建和销毁 |
| Scene | 相机、视图、场景实体 | 只能由 Unity 层管理 |

`AFrameBootstrap` 是唯一组合根，可以使用 `DontDestroyOnLoad`，但不提供静态 `Instance`。它创建子作用域、读取生成的模块注册表、校验依赖并启动会话。重复 Bootstrap、缺少服务、多个提供者、作用域越级和循环依赖都必须阻止启动。

## 数据与资源

定义类型位于 Contracts，JSON 是这些类型的可审查实例数据：

```text
Assets/Game/Features/*/Definitions/*.aframe.json
        ↓
Schema 校验、ID 校验、引用校验、版本迁移
        ↓
Generated/ 运行时数据与 ID 常量
```

生成过程必须记录源文件哈希、Schema 版本和生成器版本，并提供验证模式检查生成结果是否过期。领域读取纯 C# 定义对象，不读取 ScriptableObject。Sprite、Prefab、音频等表现资源通过独立的资源键表绑定，避免把 Unity 资源类型带入领域定义。

首版可使用 Unity 官方 Newtonsoft Json 包。完整 Schema 生成、无反射序列化和资源键自动发现属于后续阶段。

## 适配器与联机边界

物理接口拆分为只读查询和位置写入，避免 `IPhysicsQuery` 同时承担查询与修改。2D、3D 只替换 Unity 适配器，领域命令、事件和查询保持不变。

首版联机只定义主机权威接口，不承诺实时动作能力：

```text
Client: Input → CommandEnvelope → Host
Host:   Receive → Dispatch → Simulate → Events
Client: Snapshot / Event → Query → View
```

首版不实现客户端预测、回滚、锁步、延迟补偿和完整状态同步。快照协议需要在后续阶段明确稳定类型 ID、版本迁移、完整/增量快照、顺序号和传输编码后再实现。

## 规则强制

编译器和 asmdef 负责 Unity 引用边界；Editor 验证器负责模块图、重复 ID、定义文件和资源键；Roslyn 分析器在规则稳定后负责源码级规则：

- Core 不得引用 `UnityEngine`。
- 功能不得引用其他功能的非 Contracts 程序集。
- 禁止可变静态状态、单例、`Find*` 和 `SendMessage`。
- 命令与事件必须是不可变值类型。
- 领域不得读取系统时间或非托管随机数。
- 生成文件只能由导入命令更新。

首版不把文本扫描器描述成完整编译器规则。验证报告必须标注检查来源和检查范围。

## AI 协作

项目根目录维护 `AGENTS.md`，功能目录维护 README。任务文件限定目标、允许路径、只读路径、使用模块和验收命令：

```text
Tasks/<task-id>.task.json
```

`aframe check --task` 的首版实现可以先由 Unity batchmode 和 Git diff 组成，必须支持指定基线，避免用户已有修改被误判。Context 文件按功能拆分，生成文件写入 `Assets/Game/Generated/Context`，并明确哪些生成物提交 Git、哪些只在本机生成。

Editor 菜单和 CLI 必须调用同一套服务。CLI 和 Editor 命令通道属于后续阶段，不允许各自实现一套规则。

## 实施顺序

1. 在现有包结构中补齐 v2.1 文档、根目录 `AGENTS.md` 和 Contracts 边界。
2. 实现固定 Tick、CommandQueue、EventQueue、Query 和 Session 作用域。
3. 将 Character 示例迁移到 Contracts/Core/Unity 三程序集，并验证 Play Mode。
4. 将定义导入改为强类型校验、重复 ID 检查和源哈希验证。
5. 增加架构验证和 Context 生成，先保留现有 Editor 菜单。
6. 增加最小任务范围检查和 batchmode 入口。
7. 再实现 Save、Physics、UI、Audio、Camera、Network 等可选适配器。
8. 最后实现快照代码生成、联机示例、包 tag 发布和模板化。

## 首版验收

只保留必要检查：

- Unity 6.6 项目正常导入并编译。
- 架构验证能发现循环依赖、非法程序集引用和重复 ID。
- 一个文本定义能生成并加载运行时数据。
- 示例场景能进入 Play Mode 并运行 Smoke Check。
- Character 或 Inventory 的一个核心命令能通过固定 Tick 改变状态并产生事件。

首版不做大规模玩法测试、性能测试、完整网络测试或多套示例游戏。

## 当前实现与迁移

当前仓库已经有 Foundation、Runtime、Data、Unity、Editor 以及 Character 示例，但仍使用 v1 的同步模块启动、立即命令分发、Unity 时间和菜单工具。v2.1 的固定 Tick、作用域、Contracts 独立程序集、CLI、分析器和完整数据 Schema 尚未全部实现。

迁移期间以当前可编译实现为基线，每完成一个阶段更新本节和 README，避免把目标接口当成已经存在的 API。
