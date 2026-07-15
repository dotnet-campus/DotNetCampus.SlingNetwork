# NAT 探测 UDP 包设计和流向

## 分类

映射行为 Mapping：

- 端点无关 EndpointIndependent
- 地址相关 AddressDependent
- 地址和端口均相关 AddressAndPortDependent

过滤行为 Filtering：

- 端点无关 EndpointIndependent
- 地址相关 AddressDependent
- 地址和端口均相关 AddressAndPortDependent

## 数据包流向

### 准备

- 客户端 C：通过 HTTP 请求服务器 S1，从本次请求中获得 S1 本轮可供使用的两个 UDP 端点 P1 和 P2
    - 可以直接从本次请求中获得备用服务器列表，选择一个备用服务器作为 S2
    - 也可以自备服务器列表，选择一个作为备用服务器 S2
- 客户端 C：使用单个 UDP Socket，记录本地端点 C0:P0，全程确保本地端点不变

### 第 1.1 轮

- C->S1:P1 `[NAT-1S]{sessionId}{S2-HTTP}`，服务端看到客户端的公网端点并记为 C1:P1；同时服务端了解到客户端选择使用的备用服务器 S2
    - S1->S2 (HTTP) S1 请求服务器 S2 向客户端公网端点 C1:P1 发送 NAT 探测包，并在本次请求中获得了本次测试可供使用的两个 UDP 端点 P1 和 P2，在请求中返回给 S1 的同时，接下来也会将此信息通过 UDP 发给 C

### 第 1.2 轮（过滤行为测试）

任意一个

- S1:P1->C1:P1 `[NAT-1R]{sessionId}{C1:P1}{S2.P1}{S2.P2}`，每 500ms 一次，共 4 次
- S1:P2->C1:P1 `[NAT-21]{sessionId}{C1:P1}{S2.P1}{S2.P2}`，每 500ms 一次，共 4 次
- S2:P1->C1:P1 `[NAT-22]{sessionId}{C1:P1}{S2.P1}{S2.P2}`，每 500ms 一次，共 4 次
- 客户端 C 一直等待收包直到超时：
    - 收到任意一个包，验证 `C0:P0 == C1:P1`，相等说明客户端 C 直接暴露在公网，不相等说明存在 NAT（网络地址转换）
    - `[NAT-1R]` `[NAT-21]` `[NAT-22]` 的组合情况：
        - `_, true, true` => 端点无关
        - `_, true, false` => 地址相关
        - `true, false, _` or `_, false, true` => 地址和端口均相关
        - `false, false, false` => 测试无效，重测

### 第 2.1 轮

- C->S2:P1 `[NAT-3S]{sessionId}`，每 500ms 一次，共 4 次，服务端看到客户端的公网端点并记为 C2:P1

### 第 2.2 轮（映射行为测试）

- S2:P1->C2:P1 `[NAT-3R]{sessionId}{C2:P1}`，每 500ms 一次，共 4 次
- S2:P2->C2:P1 `[NAT-3R]{sessionId}{C2:P1}`，每 500ms 一次，共 4 次
- 客户端 C 一直等待收包直到超时：
    - 收到任意一个包，验证 `C1:P1 == C2:P1`，相等说明映射为「端点无关」，否则进行第 3 轮测试

---

当前 2 轮测试未得出结论时补测第 3 轮。

### 第 3.1 轮（映射行为测试补充）

- C->S2:P2 `[NAT-4S]{sessionId}`，每 500ms 一次，共 4 次，服务端看到客户端的公网端点并记为 C2:P2

### 第 3.2 轮

- S2:P2->C2:P2 `[NAT-4R]{sessionId}{C2:P2}`，每 500ms 一次，共 4 次
- 客户端 C 一直等待收包直到超时：
    - 验证 `C2:P1 == C2:P2`，相等说明映射为「地址相关」，否则说明映射为「地址和端口均相关」
