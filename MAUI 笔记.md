# MAUI APP 文件结构

- Platforms：
    - 平台特定代码，如权限设置、文件路径配置
    - 一般不手动修改，除非调用原生API

- Resources:
    - 放资源的文件夹：
        - Images：放图标图片
        - Fonts：字体
        - Styles：全局样式，如按钮颜色、字体大小

- Pages：
    - 存放所有页面的UI（XAML文件）：
        - InputPage.xaml：用于用户输入金额、类别、备注、时间。
        - StatsPage.xaml：用于图表展示（支出、收入、趋势等）。
        - 每个页面通常配有`.xaml.cs`的后台代码处理交互逻辑。

- PageModels:
    - 页面对应的逻辑控制器，绑定数据和命令。
    - MVVM架构：
        - Model - View - ViewModel
        - Model 数据结构和数据库映射
        - View（在这里是Page）UI页面
        - ViewModel（在这里是PageModel）绑定逻辑、命令、属性
        - 如`MainPage.xaml`绑定`MainPageModel.cs`，就是View和ViewModel的绑定关系，即VVM
    - 用MVVM处理大部分逻辑，如按钮点击、下拉刷新、文本显示、控件点击......；只有当MVVM难以处理或过于复杂时，才退回`.xaml.cs`做简单处理。

- Models：
    - 数据模型类。

- Services：
    - 封装与SQLite数据库的所有交互。

- Data：
    - SQLite 初始化类、连接类、常量。
    - 与数据库连接相关的配置和入口（可作为`DatabaseContext.cs`）。

- Utilities:
    - 转换器（如`InvertedBoolConverter`）、扩展方法、日志工具。
    - 提供复用性强的工具类，提高代码整洁度。

# 运行流程
## 数据处理流程
1. SeedDataService.cs   ← 加载测试数据到数据库（只在第一次运行时执行）
2. SQLite 数据库
3. RecordRepository.cs  ← 从数据库取出数据
4. MainPageModel.cs     ← 加工数据 → 提供图表/表格绑定
5. MainPage.xaml        ← 展示 UI

## 应用运行流程
```
启动程序
  ↓
MauiProgram.cs
  ├─ builder.UseMauiApp<App>()
  ├─ 注册组件 (Syncfusion, CommunityToolkit)
  ├─ 注册服务 (AddSingleton, AddTransient)
  ↓
构建 App (builder.Build())
  ↓
运行应用
```
- `MauiProgram.cs`是 `.NET MAUI` 的标准应用启动流程，它遵循类似 ASP.NET Core 的 `HostBuilder` 模式。
- `MauiProgram.cs`主要包括：
    - 创建并配置 App 实例；
    - 注册依赖服务（Service）；
    - 配置字体、组件库（Syncfusion、CommunityToolkit 等）；
    - 可注册的服务类型：
        ```
        builder.Services.AddSingleton<SomeService>();   // 全局服务
        builder.Services.AddTransient<SomePage>();       // 页面，每次导航创建新实例
        builder.Services.AddTransient<SomePageModel>();  // 页面模型（ViewModel）

        ```
    - `AddSingleton` 和 `AddTransient` 的区别：
        - `AddSingleton<T>()`	所有地方使用同一个实例，适用于共享状态或数据库连接等长生命周期资源
        - `AddTransient<T>()`	每次请求都会创建一个新实例，适用于短生命周期页面或不需要共享状态的逻辑类

最后构建并返回 MauiApp 实例。

# 账本数据
## Record
- 账户 Account
- 支出、收入 Type
- 金额 Amount
- 支出类别 Category
- 备注 Note