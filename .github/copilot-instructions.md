# SlingNetwork 开发指南

> 本文件用于存储每个 AI 协作会话都必须遵循的核心开发规范。更完整的协作规则见根目录 `AGENTS.md`。

## 项目概述

SlingNetwork 是一个 P2P 打洞组网工具。客户端主程序通过配置和操作接口控制组网能力；服务端用于可中继部署的非中心化连接协助；UI 项目用于提供可视化客户端操作界面。

## 项目结构

```text
src/SlingNetwork/         # 客户端主程序，无界面
src/SlingNetwork.Server/  # 服务端程序
src/SlingNetwork.UI/      # 客户端界面程序
samples/                  # 示例
tests/                    # 测试
docs/tasks/               # AI 辅助开发任务
docs/knowledge/           # 知识库
docs/archive/             # 归档资料
```

## 开发原则

- 不在初始化任务中实现业务逻辑。
- 优先沿用仓库既有目录、命名、MSBuild 和 CI 约定。
- 使用中央包版本管理，不在项目文件中写具体版本。
- 所有网络、IPC、打洞、服务端中继相关变更必须配套测试或明确说明测试缺口。
- 不持久化用户连接数据，除非任务文档明确要求并说明隐私边界。

