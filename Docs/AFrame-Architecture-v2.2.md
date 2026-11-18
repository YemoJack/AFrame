# AFrame 通用模块化 Unity 框架 v2.2

## 定位

AFrame 面向中小型 Unity 项目，结构为“核心运行时 + 可选适配器 + 项目功能”。2D、3D、单机和联机通过适配器与配置组合。玩法规则放在不引用 Unity 的 C# 程序集中。

v2.2 是可实施版本。它在 v2.1 的基础上做了四件事：

- 补齐首版示例需要的能力。
- 消除几处规则冲突。
- 给出运行时默认值。
- 把规则检查改为基于编译结果的精确检查。

网络预测、回滚、完整代码生成和复杂场景自动化仍然推迟。

本文档的前 14 节描述 v2.2 的目标架构，第 15 节记录当前单机版本的实现与差异。

---

## 1. 首版范围

首版交付以下能力：

1. 程序集边界：Foundation、Runtime、Data、Unity、Editor；每个功能拆成 Contracts / Core / Unity 三个程序集。
2. 启动：App / Session 两级作用域、模块图、启动校验、自动创建的 Bootstrap。
3. 运行时：固定 Tick、命令队列、事件队列、查询接口、输入源。
4. 视图：视图绑定（SceneScope、视图工厂）和表现插值。
5. Character 示例：位置由领域计算，只做平面移动，不做碰撞。
6. 文本定义导入：强类型校验、稳定 ID、格式版本、源文件哈希。
7. 工具：架构验证、Context 生成、任务范围检查、batchmode 入口、最小 Editor 命令通道。
8. 测试：EditMode 测试工具 TestGame 和 Play Mode Smoke Check。

以下内容放到后续阶段：

- Save 与快照协议
- Physics 2D/3D、UI、Audio、Camera、正式的 Input 模块
- 快照代码生成、Network
- Roslyn 分析器、功能脚手架、模板化

---

## 2. 包与程序集

```text
Packages/
  com.aframe.core/
    Runtime/Foundation/AFrame.Foundation.asmdef
    Runtime/Runtime/AFrame.Runtime.asmdef
    Runtime/Data/AFrame.Data.asmdef
    Runtime/Data/Unity/AFrame.Data.Unity.asmdef
    Runtime/Unity/AFrame.Unity.asmdef
  com.aframe.editor/
    Editor/AFrame.Editor.asmdef
  com.aframe.modules/          # 可选，包含 Input、Save、Physics 和 Network
```

包定义安装和分发范围。各 `.asmdef` 继续约束代码的引用方向。Editor 和 Modules 依赖 Core，默认只安装 Core 与 Editor。

| 程序集                 | 可以引用                                                     | noEngineReferences |
| ------------------- | -------------------------------------------------------- | ------------------ |
| `AFrame.Foundation` | 无                                                        | 是                  |
| `AFrame.Runtime`    | Foundation                                               | 是                  |
| `AFrame.Data`       | Foundation                                               | 是                  |
| `AFrame.Data.Unity` | Foundation、Data                                           | 否                  |
| `AFrame.Unity`      | Foundation、Runtime、Data、Data.Unity                       | 否                  |
| `AFrame.Editor`     | 以上全部（仅编辑器）                                               | 否                  |
| `<F>.Contracts`     | Foundation、Runtime、Data、其他功能的 Contracts                  | 是                  |
| `<F>.Core`          | 上一行全部 + 本功能的 Contracts                                   | 是                  |
| `<F>.Unity`         | Foundation、Runtime、Data、AFrame.Unity、本功能和其他功能的 Contracts | 否                  |
| `<F>.Tests`         | 本功能的 Contracts 和 Core、Runtime、TestGame                   | 否                  |
| `Game.Composition`  | 所有功能的 Core 和 Unity、AFrame.Unity                          | 否                  |

规则：

- 只有 `Game.Composition` 和 `<F>.Tests` 可以引用功能的 Core。
- 功能之间只能引用对方的 Contracts。
- Input System、Addressables、Unity Transport 等可选依赖，不得进入核心程序集，也不得进入功能的 Contracts 和 Core。

---

## 3. 功能结构

```text
Assets/Game/
  Composition/                       # Game.Composition.asmdef
    Generated/                       # 模块注册表、link.xml
  Features/
    Character/
      Contracts/                     # Character.Contracts.asmdef
        Commands/
        Events/
        Queries/
        Inputs/
        DefinitionTypes/             # 定义类型（C#）
        Generated/                   # 定义 ID 常量
      Core/                          # Character.Core.asmdef
        Domain/
        Application/
        CharacterModule.cs           # Core 中唯一的 public 类型
      Unity/                         # Character.Unity.asmdef
        Views/
        Adapters/
        CharacterAdapterModule.cs    # 提供输入源等端口实现
      Tests/                         # Character.Tests.asmdef（EditMode）
      Definitions/                   # *.aframe.json 数据源文件
      Generated/                     # 规范化数据，只放数据不放代码
      feature.json
      README.md
  Scenes/
  ProjectConfig/
    Resources/AFrameGameProfile.asset
Context/                             # 项目根目录，在 Assets 之外
Tasks/
Tools/aframe                         # CLI 脚本
AGENTS.md
```

- **Contracts**：功能唯一的对外 API，包括命令、事件、查询接口、输入结构和定义类型。
- **Core**：只有模块入口类是 public，其余都是 internal。
- **Unity**：可以包含一个适配器模块，为 Core 需要的端口提供实现，例如输入源。
- **生成代码**：必须放在所属程序集目录下的 `Generated/`。功能根目录的 `Generated/` 只放数据。

`feature.json` 只记录无法从代码和目录推导出来的信息：

```json
{
  "id": "character",
  "version": "0.1.0",
  "summary": "玩家角色：平面移动与冲刺",
  "physicsAuthority": "domain",
  "supports": { "dimensions": ["2d", "3d"], "network": ["offline"] }
}
```

- `physicsAuthority` 有三个取值：
  - `none`：功能没有位置概念。
  - `domain`：位置由领域计算。
  - `engine`：位置由 Unity 物理决定，后续阶段支持。
- Profile 启用的功能，必须支持 Profile 设定的维度和网络模式，否则验证失败。

---

## 4. 运行时模型

### 4.1 数据流

```text
写        Input / UI / Network → Command → CommandQueue → Handler → Domain 状态
通知      Domain → DomainEvent → EventQueue → 领域订阅者 / View / Save / Network
读        View / UI → Query → 只读状态
连续输入  Unity 采样 → IInputSource<T> → FixedTick
```

### 4.2 Tick 循环

```text
Update        采样输入，推进累加器
Tick N
  1. Receive   冻结本 Tick 的输入；取出待执行命令，分配 Tick 和全局 Sequence
  2. Dispatch  按 Sequence 执行命令；失败时产生 CommandRejected
  3. Simulate  按模块顺序执行 FixedTick
  4. Flush     分发事件，直到队列为空
  Tick 结束后  调用 View.OnTickCompleted；需要时执行快照钩子（后续阶段）
LateUpdate    调用 View.OnRender(alpha)，在上一 Tick 和当前 Tick 之间插值
```

### 4.3 默认值

| 项              | 默认值                                                  |
| -------------- | ---------------------------------------------------- |
| Tick 频率        | 50Hz，可在 Profile 中配置                                  |
| 单帧最多补几个 Tick   | 5 个；超出时丢弃剩余的累积时间，并记录警告                               |
| 单个 Tick 耗时超过步长 | 记录警告，不中断运行                                           |
| 暂停             | 停止累加器；命令照常入队，恢复后执行                                   |
| 迟到命令           | 单机不存在；联机时主机在当前 Tick 执行，日志中保留原 Tick                   |
| 单次 Flush 事件上限  | 1024 个；超出视为错误                                        |
| Handler 抛出异常   | 开发版：中断会话并输出报告；发布版：拒绝该命令（错误码 `aframe.exception`）并记录日志 |

### 4.4 执行顺序

- 模块顺序：按依赖拓扑排序；顺序相同时按 ModuleId 的字典序。
- Dispatch：按 Sequence 顺序执行。
- FixedTick：先按模块顺序，同一模块内按注册顺序。
- Flush：每个事件先交给领域订阅者（按模块顺序），再交给 View 订阅者（按绑定顺序）。

### 4.5 命令与事件规则

- 命令不会立即执行。任何时候提交的命令，都进入下一次 Dispatch。
- Handler 先校验、再修改状态。校验失败时返回 `Result.Fail(ErrorCode)`，不修改任何状态。
- 领域订阅者在 Core 中注册，只能修改本功能的状态。它可以继续发事件，新事件追加到队尾。
- View 订阅者只能做两件事：播放一次性表现（音效、特效），或提交命令。持续变化的状态在 `OnTickCompleted` 中通过查询读取。
- 命令和事件都是 readonly struct，并带有 `[StableId]`。

### 4.6 读模型

- 查询接口继承 `IQuery`，放在 Contracts 中，返回不可变值或只读视图。
- 查询不能修改状态，修改只能通过命令。
- View 只能获取 `IQuery` 的子接口。

---

## 5. 核心接口

首版冻结以下接口：

```csharp
// ---------- Foundation ----------
// ModuleId、PlayerId、EntityId、ViewKey、ErrorCode（稳定字符串，如 "character.dash_on_cooldown"）
// Result、ILog、IRandom（可设种子，状态可读写）

[AttributeUsage(AttributeTargets.Struct)]
public sealed class StableIdAttribute : Attribute
{
    public StableIdAttribute(string id) { Id = id; }
    public string Id { get; }
}

// ---------- 模块 ----------
public enum ModuleScope { App, Session }

public interface IGameModule
{
    ModuleId Id { get; }
    ModuleScope Scope { get; }
    void Configure(IModuleBuilder builder);
    void Start(ModuleContext context);
    void Stop();
}

public interface IModuleBuilder
{
    void Require<TService>() where TService : class;
    void Provide<TService>(Func<IServiceResolver, TService> factory) where TService : class;
    void ProvideMany<TService>(Func<IServiceResolver, TService> factory) where TService : class;
    void Handle<TCommand>(Func<IServiceResolver, ICommandHandler<TCommand>> factory)
        where TCommand : struct, ICommand;
    void Subscribe<TEvent>(Func<IServiceResolver, IEventSubscriber<TEvent>> factory)
        where TEvent : struct, IDomainEvent;
    void Tick(Func<IServiceResolver, IFixedTickable> factory);
}

// 只在模块注册工厂和组合根中使用；领域对象通过构造函数接收依赖
public interface IServiceResolver
{
    T Get<T>() where T : class;
    IReadOnlyList<T> GetAll<T>() where T : class;
}

// ---------- 命令 ----------
public interface ICommand { }

public readonly struct CommandEnvelope<T> where T : struct, ICommand
{
    public readonly PlayerId Issuer;
    public readonly long Tick;       // Receive 阶段分配
    public readonly long Sequence;   // 全局递增，决定执行顺序
    public readonly T Payload;

    public CommandEnvelope(PlayerId issuer, long tick, long sequence, in T payload)
    {
        Issuer = issuer;
        Tick = tick;
        Sequence = sequence;
        Payload = payload;
    }
}

public interface ICommandHandler<T> where T : struct, ICommand
{
    Result Handle(in CommandEnvelope<T> command);
}

public interface ICommandSink
{
    void Submit<T>(PlayerId issuer, in T command) where T : struct, ICommand;
}

// ---------- 事件 ----------
public interface IDomainEvent { }

public interface IEventSink
{
    void Raise<T>(in T domainEvent) where T : struct, IDomainEvent;
}

public interface IEventSubscriber<T> where T : struct, IDomainEvent
{
    void On(in T domainEvent);
}

// 框架内置事件：CommandRejected、EntitySpawned(EntityId, ViewKey)、EntityDespawned(EntityId)

// ---------- 查询 ----------
public interface IQuery { }   // 标记接口：View 只能获取它的子接口

// ---------- 时间与输入 ----------
public readonly struct TickContext
{
    public long Tick { get; }
    public float DeltaTime { get; }

    public TickContext(long tick, float deltaTime)
    {
        Tick = tick;
        DeltaTime = deltaTime;
    }
}

public interface IFixedTickable
{
    void FixedTick(in TickContext context);
}

public interface IInputSource<TInput> where TInput : struct
{
    TInput Get(PlayerId player);   // 返回本 Tick 已冻结的输入
}
```

Unity 层：

```csharp
public interface IViewContext
{
    EntityId Entity { get; }   // 场景中的 View 为 EntityId.None
    T Query<T>() where T : class, IQuery;
    ICommandSink Commands { get; }
    void Subscribe<TEvent>(IEventSubscriber<TEvent> subscriber)
        where TEvent : struct, IDomainEvent;   // Unbind 时自动取消
}

public abstract class AFrameView : MonoBehaviour
{
    public abstract void Bind(IViewContext context);
    public virtual void Unbind() { }
    public virtual void OnTickCompleted() { }      // Flush 之后：从查询读取新状态
    public virtual void OnRender(float alpha) { }  // LateUpdate：插值
}
```

- 网络传输接口不冻结。联机相关只冻结 `CommandEnvelope` 的字段。
- `Snapshot` 注册和 `ISnapshotParticipant` 在步骤 7 加入。

---

## 6. 作用域、组合根与视图绑定

### 6.1 作用域

| 作用域     | 示例                   | 规则                           |
| ------- | -------------------- | ---------------------------- |
| App     | 日志、定义注册表、资源键表、设置     | 不依赖 Session 服务               |
| Session | 角色、背包、任务             | 随一局游戏创建和销毁                   |
| Scene   | SceneScope、场景中的 View | 只由 Unity 层管理，不属于 IGameModule |

- 服务默认只有一个提供者。集合服务用 `ProvideMany` 声明，用 `GetAll` 获取。
- 所有服务都是所在作用域内的单例，不提供瞬时服务。
- 外层作用域不能依赖内层作用域。

### 6.2 Bootstrap

**创建方式**

- 用 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` 读取 `Resources/AFrameGameProfile`，然后创建唯一的 `AFrameBootstrap`。
- 它会设置 `DontDestroyOnLoad`，但不提供静态 `Instance`。
- 场景和 Prefab 中不放 Bootstrap，由验证器检查。这样从任意场景进入 Play Mode 都能正常启动。

**启动流程**

1. 读取生成的模块注册表。
2. 调用各模块的 Configure。
3. 校验：缺少服务、多个提供者、作用域越级、循环依赖、命令处理器不唯一、StableId 重复。
4. 拓扑排序。
5. 依次调用 Start。
6. 启动 Tick。

任何一步失败，都阻止启动并输出报告。

**其他规则**

- 异步资源（定义数据、资源键表）由 Unity 适配器在创建 Session 之前加载完成。模块的 `Start` 保持同步，只接收已经加载好的数据。
- 框架代码和游戏代码都不使用可变静态状态。如果框架确实需要静态状态，只能放在框架程序集里，在规则配置中显式列出，并在 `SubsystemRegistration` 阶段重置，保证关闭 Domain Reload 时也能正确运行。

### 6.3 视图绑定

- **场景中的物体**
  - 每个玩法场景的根节点下最多放一个 `SceneScope`。
  - 场景加载时（`SceneManager.sceneLoaded`），Bootstrap 通过 `scene.GetRootGameObjects()` 找到 SceneScope。
  - SceneScope 收集自身层级下的 `AFrameView` 并逐个 Bind；场景卸载时 Unbind。
- **运行时生成的物体**
  - 领域发出 `EntitySpawned(EntityId, ViewKey)`。
  - 视图工厂通过资源键表找到 Prefab，实例化后 Bind。
  - 收到 `EntityDespawned` 时 Unbind 并回收。
- 全程不使用 `Find*`、单例或反射注入。View 不包含规则。

---

## 7. Character 示例（首版）

| 程序集       | 内容                                                                                                                                                  |
| --------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| Contracts | `CharacterInput`（`Move: System.Numerics.Vector2`）、`Dash` 命令、`CharacterDashed` 事件、`ICharacterQuery`（位置、朝向）、`CharacterDefinition`（速度、冲刺距离、冷却、ViewKey） |
| Core      | `CharacterModule`（Session 作用域）：FixedTick 根据输入移动角色；`Dash` 处理器检查冷却；Session 启动时发出 `EntitySpawned`                                                      |
| Unity     | `CharacterAdapterModule` 提供 `IInputSource<CharacterInput>`；`CharacterView` 在 OnTickCompleted 中记录上一 Tick 和当前 Tick 的位置，在 OnRender 中插值                 |
| Tests     | 输入 → 位置变化；`Dash` 成功产生事件；冷却期间 `Dash` 被拒绝                                                                                                             |

- `physicsAuthority: domain`：只做平面移动，不做碰撞。碰撞在 Physics 阶段加入。
- 领域使用平面坐标：2D 视图映射到 XY 平面，3D 视图映射到 XZ 平面。用这种方式低成本地验证 2D/3D 切换。

---

## 8. 数据与资源

```text
Features/*/Definitions/*.aframe.json
        ↓ Newtonsoft 强类型反序列化（MissingMemberHandling.Error）
        ↓ C# 校验：必填、取值范围、ID 唯一、跨文件引用、格式版本迁移
        ↓
Contracts/Generated/     定义 ID 常量
Generated/               规范化数据（TextAsset）+ 来源记录
Composition/Generated/   link.xml
```

```json
{
  "version": 1,
  "characters": [
    {
      "id": "player",
      "moveSpeed": 5.0,
      "dashDistance": 3.0,
      "dashCooldown": 1.0,
      "view": "character/player"
    }
  ]
}
```

**定义与生成**

- 定义类型放在 `Contracts/DefinitionTypes/`，是纯 C# 类型。首版不使用多态。
- 来源记录包含源文件哈希、格式版本和生成器版本。验证模式会检查生成结果是否过期，或是否被手改。

**运行时加载**

- 由 `AFrame.Unity` 读取 TextAsset，反序列化后注册到 App 作用域的 `IDefinitions`。
- 领域只读取纯 C# 定义对象，不读取 ScriptableObject。
- Newtonsoft 依赖反射。生成的 `link.xml` 保留所有 `*.Contracts` 程序集，防止被 IL2CPP 裁剪。

**表现资源**

- 资源键表是 Unity 层的 ScriptableObject，负责把 `ViewKey` 映射到 Prefab、Sprite、音频。
- 验证器检查定义中引用的资源键是否都存在。

**后续阶段**

- 首版不生成 JSON Schema 文件。
- Schema 生成、无反射序列化、Addressables 接入都放到后续阶段。

---

## 9. 存档与快照（步骤 7）

```csharp
public interface ISnapshotParticipant
{
    string Key { get; }
    int Version { get; }
    void Write(ISnapshotWriter writer);
    void Read(ISnapshotReader reader, int version);   // 旧版本数据在这里迁移
}
```

- 步骤 7 先让各功能手写带版本的 Write/Read，Save 直接基于它实现。步骤 8 的代码生成只负责把这部分自动化。
- 快照在 Tick 结束后按需生成，不作为 Tick 的一个阶段。
- 存档、联机中途加入和调试使用同一种快照格式。

---

## 10. 联机边界（后续阶段）

默认模型是主机说了算：

```text
客户端：Input → Payload + 客户端序号 → 主机
主机：  Receive（生成 CommandEnvelope）→ Dispatch → Simulate → Flush → 广播事件和快照
客户端：先应用快照，再分发事件 → Query → View
```

**运行方式**

- 客户端运行相同的模块，但不执行命令和 Tick。客户端的状态只由主机快照写入。

**物理接口**

- 只读查询用 `IPhysicsQuery`。
- 位置读写用 `IPhysicsBodies`，只在 `engine` 模式下使用。

**不实现的内容**

- 客户端预测、回滚、锁步、延迟补偿、完整状态同步。

**实现前需要明确**

- 稳定类型 ID（已由 StableId 提供）
- 版本迁移
- 完整快照和增量快照
- 顺序号
- 传输编码

---

## 11. 规则强制

| 规则                                                                                                          | 首版检查方式                    |
| ----------------------------------------------------------------------------------------------------------- | ------------------------- |
| Foundation、Runtime、Data、Contracts、Core 不引用 UnityEngine                                                      | `noEngineReferences`（编译器） |
| 功能只引用其他功能的 Contracts；只有 Composition 和 Tests 引用 Core                                                         | 读取 asmdef                 |
| Core 中只有模块入口类是 public                                                                                       | 反射                        |
| 禁止可变静态字段                                                                                                    | 反射                        |
| 命令和事件是 readonly struct，并带有 StableId                                                                         | 反射                        |
| StableId、定义 ID、ErrorCode 不重复                                                                                | 反射 + 定义校验                 |
| 禁止调用 `GameObject.Find*`、`Object.Find*`、`SendMessage`、`DateTime.Now`、`System.Random`、`Environment.TickCount` | Mono.Cecil 扫描 IL          |
| Generated 和 Context 不手改、不过期                                                                                 | 哈希                        |
| 场景和 Prefab 中没有 Bootstrap；每个场景最多一个根 SceneScope                                                               | 打开场景检查                    |
| Profile 与 `feature.json` 中的 supports 匹配                                                                     | 验证器                       |

- 验证器检查的是编译后的程序集（`Library/ScriptAssemblies`），不做源码文本扫描。Mono.Cecil 来自 Unity 官方包 `com.unity.nuget.mono-cecil`。
- 验证报告中，每条结果都标注检查来源（compiler / asmdef / reflection / cecil / hash / scene）和检查范围。
- Roslyn 分析器在后续阶段实现同样的规则，只用于 IDE 实时提示。

---

## 12. AI 协作

### 12.1 文件

**`AGENTS.md`** 包含以下内容：

- 规则摘要（第 4、6、11 节）
- 目录结构
- 命令表
- 新增功能流程
- 各功能 README 的链接

**`Context/`**（位于 Assets 之外）：

- 每次编译后自动生成，排序固定。
- 提交到 Git，让无法运行 Unity 的 AI 也能读取。
- `check` 会检查它是否过期。

```text
Context/
  ProjectMap.json          目录、功能列表、场景、Profile
  ModuleGraph.json         模块、作用域、服务的提供与依赖
  Features/<Name>.json     公共 API：命令、事件、查询、输入、定义类型、依赖
  ValidationReport.json    最近一次验证结果
```

**`Tasks/<id>.task.json`**：

```json
{
  "id": "character-dash",
  "goal": "角色支持冲刺，冷却 1 秒",
  "allowedPaths": ["Assets/Game/Features/Character/**"],
  "readOnlyPaths": ["Packages/**", "Assets/Game/ProjectConfig/**"],
  "features": ["character"],
  "acceptance": ["Character.Tests.Dash*"]
}
```

### 12.2 命令

```text
aframe task start <id>      记录任务基线
aframe compile              编译
aframe validate             架构验证
aframe test [--feature X]   运行 EditMode 测试
aframe context              生成 Context
aframe smoke                Play Mode Smoke Check
aframe check --task <id>    范围检查 + compile + validate + 验收测试
```

- 每个命令都返回退出码，结果写入 `Logs/AFrame/<command>.json`。
- Editor 菜单、batchmode 和命令通道调用的是同一套 Editor 服务，不各自实现规则。
- **Editor 没有打开时**：通过 batchmode 的 `-executeMethod` 执行。
- **Editor 已经打开时**（检测到 `Temp/UnityLockfile`）：
  - CLI 把请求写入 `Library/AFrame/Requests/`。
  - Editor 中的监听器主动调用 `AssetDatabase.Refresh` 和 `CompilationPipeline.RequestScriptCompilation`，不依赖窗口是否获得焦点，然后把结果写回。
  - 请求状态保存在文件中，Domain Reload 之后可以继续执行。
- 命令通道只读写本机文件，不包含任何模型客户端。

### 12.3 任务范围检查

1. `task start` 把基线写入 `Library/AFrame/Tasks/<id>.baseline.json`，内容为：当前提交，以及当时已修改文件的哈希。
2. `check` 先做不依赖 Unity 的部分：对比基线和当前文件，找出本次任务改动的文件，检查是否越出 `allowedPaths`、是否改了 `readOnlyPaths`。用户在任务开始前就有的修改不会被误判。
3. `Generated/` 和 `Context/` 允许变化，但内容必须与重新生成的结果一致。
4. 再执行 compile、validate 和 `acceptance` 中列出的测试。
5. 最后把变更清单写入 `Logs/AFrame/task-<id>.json`。

### 12.4 新增功能流程

1. 阅读 `AGENTS.md` 和相关的 `Context/Features/*.json`。
2. 先写 Contracts：命令、事件、查询、输入、定义类型。
3. 写 EditMode 测试，再实现 Core。
4. 写 Unity 层的 View 和适配器模块。
5. 运行 `aframe check --task <id>`。

---

## 13. 实施顺序

| 步骤 | 内容                                                                  | 完成标准                                                                         |
| -- | ------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| 1  | 写 v2.2 文档和 `AGENTS.md`；把 Character 拆成三个程序集；创建 Composition           | 编译通过；在 Core 或 Contracts 中引用 UnityEngine 会编译失败                                |
| 2  | 实现固定 Tick、命令队列、事件队列、查询、App/Session 作用域、启动校验、自动创建 Bootstrap、TestGame | EditMode 测试覆盖：命令在下一 Tick 执行、失败时产生 CommandRejected、执行顺序符合 4.4 节、缺少或重复提供者时阻止启动 |
| 3  | 迁移 Character：输入源、领域移动、视图绑定、插值                                       | `aframe test` 通过；从任意场景进入 Play Mode，角色都能生成并平滑移动                               |
| 4  | 数据管线：强类型导入、ID 常量、源文件哈希、link.xml、资源键表                                | 一个 `.aframe.json` 能生成数据并在 Play Mode 中加载；手改 Generated 后 validate 失败           |
| 5  | 验证器（asmdef、反射、Cecil、场景检查）、Context 生成、Profile 校验                     | 一组故意违规的样例被逐条检出；Context 过期能被检出                                                |
| 6  | 任务基线与检查、batchmode 入口、最小命令通道                                         | Editor 开着和关着都能运行 `check`；越界修改能被检出；用户已有的修改不被误判                                |
| 7  | Save 与手写快照；Physics、UI、Audio、Camera、正式 Input 模块                      | 存档能读写，旧版本数据能迁移；2D/3D 物理适配器可以切换                                               |
| 8  | 快照代码生成、Network 主机权威示例、Roslyn 分析器、包 tag 发布、模板化                       | 单机与联机切换后，示例仍能运行                                                              |

步骤 1–6 是首版。

---

## 14. 首版验收

- Unity 6.6 项目能正常导入并编译。
- 架构验证能检出：循环依赖、非法程序集引用、重复 ID、缺少 StableId、违规 API 调用、Generated 或 Context 过期或被手改。报告中标注了检查来源。
- 一个 `.aframe.json` 能生成规范化数据，并在 Play Mode 中加载。
- EditMode 测试：Character 的一个命令在下一 Tick 执行，改变状态并产生事件；失败时产生 CommandRejected。
- Play Mode Smoke：从任意场景进入后，Bootstrap 自动创建，角色视图生成并平滑移动，运行 N 帧无异常。
- 2D 和 3D 视图切换后，同一套领域代码仍能运行。
- 无论 Editor 是否打开，`aframe check --task` 都能运行，并能检出越界修改。

首版不做大规模玩法测试、性能测试、完整网络测试，也不做多套示例游戏。

---

## 15. 当前实现与迁移

能源回收站已验证 macOS arm64 Mono 单机流程。首版的运行命令、已有证据和待手工确认事项见 [v2.2 首版验收清单](AFrame-v2.2-Acceptance.md)。

| 范围 | 当前实现 |
| --- | --- |
| 分包与装配 | Core、Editor、Modules 三个本地 UPM 包保留独立程序集。保留 `IGameModule.Dependencies`、非泛型 `CommandEnvelope` 和兼容的队列入口。新增 `Require/Provide/ProvideMany` 及工厂依赖校验。App 与 Session 分容器，Scene 由 Unity 管理。 |
| 启动与失败 | 从 Resources 自动加载 Profile 并创建持久宿主。启动失败逆序清理，未处理的 Tick 异常停止当前会话并记录现场。业务错误返回 `Result.Fail`。 |
| 输入与视图 | 帧输入在 Tick 前冻结。领域事件先于视图分发，Tick 后读取查询并按累加器插值。场景视图解绑，2D 示例通过 Profile 的 Prefab 键生成角色。两个示例共用 Character Core，分别映射 XY 与 XZ。冲刺仍为按键持续状态。 |
| 文本配置 | 保留 `id/schemaVersion/type` 头及 ScriptableObject 注册表。导入检查跨文件类型引用，支持按类型逐级迁移，产出 Contracts ID 常量与 `link.xml`。Character 速度由文本驱动。 |
| AI 工具 | 识别 `feature.json`、程序集、稳定 ID、定义、Profile 支持范围及生成内容完整性。使用 Cecil 扫描 Player 程序集，并预览场景与 Prefab 校验 Bootstrap 和 SceneScope。CLI 支持 TestGame 的 EditMode 用例、任务基线及实际 Play Mode Smoke，Context 通过菜单或 CLI 更新。 |
| 存档 | Modules 内的 Save 程序集提供原子 JSON 文件写入。示例使用手写快照和一个存档槽，先校验再恢复。 |
| 接入 | 安装脚本默认复制 Core、Editor 和 CLI。选择 `--modules` 安装可选模块，选择 `--sample` 同时安装可选模块和示例。编辑器初始化生成默认 Profile、最小场景及任务模板。 |

定义源可选填 `"references": [{"id": "character.default", "type": "character"}]`。导入会验证目标存在且类型一致。schema 0 的 Character 用 `moveSpeed`，导入时迁移到 schema 1 的 `speed`，原文件保留。其他类型在编辑器可见程序集中实现 `IDefinitionMigration` 声明单步迁移。生成常量按所属 Contracts 程序集归档，空项目无需生成空常量类。

Profile 用 `dimension`、`networkMode` 与 `enabledFeatures` 声明支持范围。使用自有场景入口时可声明 `Features` 和 `DimensionOverride`。现有 Profile 的资源键绑定用于 Prefab；Sprite、音频仍由 Unity 资源引用管理。

`Tools/aframe test [--feature character]` 运行项目的 EditMode 测试。`check` 读取任务中测试全名或通配符，零用例按失败处理。自然语言条目仍保留为手工验收。实际发布还需目标平台检查，包括 IL2CPP 的裁剪验证。全局 ErrorCode 注册表也尚未覆盖。物理、正式联机适配、正式发布继续按后续路线安排。

---

## 附：相对 v2.1 的变更

- 首版示例定为 Character：位置由领域计算、只做平面移动、不做碰撞；补上输入源、插值和视图绑定。
- 恢复 SceneScope、视图工厂和 `IViewContext` 方案。
- Bootstrap 改为通过 `RuntimeInitializeOnLoadMethod` 自动创建，不再需要静态标志或 `Find*`。
- 补齐冻结接口：`IModuleBuilder`、`ICommandSink`、事件订阅、`IQuery`、`TickContext`、`IInputSource`；同时移除了 `ICommandTransport`。
- `noEngineReferences` 扩展到 Data 和 Contracts；逐个程序集写明引用规则；只有 Composition 和 Tests 能引用 Core。
- 事件订阅者分为领域订阅者和 View 订阅者两类，并明确了 Flush 的循环规则。
- 服务默认只有一个提供者，集合服务用 `ProvideMany`；删除“生命周期”这个概念。
- Context 移到项目根目录，提交到 Git，并检查是否过期。
- 定义类型目录改名为 `DefinitionTypes/`；生成的代码放进所属程序集的目录。
- 给出 Tick、暂停、迟到命令、异常处理和执行顺序的默认值。
- 首版用手写的 StableId 代替代码生成；Schema 校验改为强类型反序列化 + C# 校验；用 link.xml 解决 IL2CPP 裁剪问题。
- 快照协议提前到 Save 所在的步骤 7，快照不再作为 Tick 的一个阶段。
- 规则检查改为基于反射和 Mono.Cecil 的精确检查；Roslyn 分析器推后。
- 任务检查加入基线机制；最小命令通道提前到首版。
- 保留 TestGame；每个实施步骤都有完成标准；新增迁移对照表。
