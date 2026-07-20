# Sling Network 项目初始化

本项目是一个 P2P 打洞组网的工具。取名 Sling，指的是无法直连到目标的设备，扔出吊索与目标挂在一起建立连接。

## 项目结构

- `src/`：源代码目录
    - `SlingNetwork/`：主程序（客户端），包含打洞组网的全部核心代码，无界面；通过「配置 + 操作」进行控制，通过 CLI/IPC/HTTP 多种方式提供操作接口；可直接 CLI 启动运行，也可加入系统服务后台运行
    - `SlingNetwork.Server/`：主程序（服务端），允许中继部署的非中心化服务器，保证隐私和安全，承诺不持久化存储任何用户数据，运行时仅在内存中保存连接必要的信息
    - `SlingNetwork.UI/`：用户界面程序（客户端），提供对主程序的可视化操作界面
- `docs/`：文档目录
    - `tasks/`：AI 辅助开发的任务文件夹
    - `knowledge/`：知识库
    - `archive/`：归档，不再使用（可能也不再可信），但仍具有参考价值的资料
- `tests/`：测试代码目录

## 项目依赖

本项目可能会用到以下依赖：

- `DotNetCampus.CommandLine`：本组织的命令行库
- `DotNetCampus.Ipc`：本组织的进程间通信库
- `TouchSocket.Http`：用于处理 HTTP 请求
- `DotNetCampus.LatestCSharpFeatures`：保证可持续使用最新的 C# 特性
