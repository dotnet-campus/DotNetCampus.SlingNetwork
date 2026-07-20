# SlingNetwork

[![.NET Build](https://github.com/dotnet-campus/SlingNetwork/actions/workflows/dotnet-build.yml/badge.svg)](https://github.com/dotnet-campus/SlingNetwork/actions/workflows/dotnet-build.yml)

SlingNetwork 是一个 P2P 打洞组网工具。名称 Sling 取自“吊索”：当设备无法直连目标时，尝试抛出一条连接吊索，将双方挂接到同一个网络连接中。

## Repository

- `src/SlingNetwork/`：客户端主程序。
- `src/SlingNetwork.Server/`：服务端程序。
- `src/SlingNetwork.UI/`：客户端界面程序。
- `tests/`：测试项目。
- `samples/`：示例。
- `docs/`：任务、知识库和归档资料。

## Build

```powershell
dotnet build --configuration release
dotnet test --configuration release --no-build
```

