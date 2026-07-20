# AGENTS.md

## 协作范围

本文件适用于整个 SlingNetwork 仓库。AI 助手在本仓库内工作时，应优先遵循本文件和 `.github/copilot-instructions.md`。

## 项目定位

SlingNetwork 是一个 P2P 打洞组网工具：

- `src/SlingNetwork/`：客户端主程序，无界面，通过配置和 CLI/IPC/HTTP 等操作接口控制。
- `src/SlingNetwork.Server/`：可中继部署的非中心化服务端，只保存运行期连接必要信息。
- `src/SlingNetwork.UI/`：客户端可视化界面。

## 工作规则

- 初始化阶段只维护基础设施，不实现业务逻辑代码。
- 修改代码前先阅读相关任务文档和邻近实现。
- 保持变更范围收敛，不做无关重构。
- 使用 `Directory.Packages.props` 管理 NuGet 包版本。
- 新增可构建项目时，应加入 `SlingNetwork.slnx`，并确保 `dotnet build` 可通过。
- 新增测试项目时，应放在 `tests/`，并确保 CI 的 `dotnet test` 可运行。
- 涉及隐私、安全、连接状态持久化的变更，必须在任务文档中明确设计依据。

## 常用命令

```powershell
dotnet build --configuration release
dotnet test --configuration release --no-build
```

