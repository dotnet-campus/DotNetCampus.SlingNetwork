# 在远程 VPS 上调试服务端

假定 `vps-config-name` 为 VPS 配置名，也可以是 `user@server-ip`，`admin` 为目标用户名。

```powershell
# 对于不支持交叉编译的平台，暂时禁用 AOT 发布
dotnet publish .\src\SlingNetwork\ -r linux-x64 -p:PublishAot=false,PublishSingleFile=true --sc
# 拷贝到目标 VPS
scp .\artifacts\publish\SlingNetwork\release_linux-x64\sling vps-config-name:/home/admin/DotNetCampus.SlingNetwork/sling
# 连接到 VPS
ssh vps-config-name
```

```bash
# 增加防火墙例外 HTTP

# 1. 确认 Docker Compose 所设的 `proxy` 网络参数
docker network inspect proxy \
  --format 'Subnet={{(index .IPAM.Config 0).Subnet}} Gateway={{(index .IPAM.Config 0).Gateway}}'

# 预期输出（设置代理和防火墙时要用到此网关 IP）
# Subnet=172.18.0.0/16 Gateway=172.18.0.1

# 2. 再确定对应的 Linux 网桥名称
BRIDGE=$(docker network inspect proxy \
  --format '{{index .Options "com.docker.network.bridge.name"}}')

if [ -z "$BRIDGE" ]; then
  BRIDGE="br-$(docker network inspect proxy --format '{{.Id}}' | cut -c1-12)"
fi

echo "$BRIDGE"
ip address show "$BRIDGE"

# 3. 然后添加 Docker 网桥的 UFW 规则
sudo ufw allow in \
  on "$BRIDGE" \
  from 172.18.0.0/16 \
  to 172.18.0.1 \
  port 5451 \
  proto tcp \
  comment 'NPM proxy to host service'

# 增加防火墙例外 UDP
sudo ufw allow \
  proto udp \
  from any \
  to any \
  port 50000:51000 \
  comment 'SlingNetwork UDP punch range'
```

```bash
# 转到调试目标文件夹
cd ./DotNetCampus.SlingNetwork
# 增加执行权限
chmod +x ./sling
# 禁用区域文化，否则会提示
# Couldn't find a valid ICU package installed on the system. Please install libicu (or icu-libs) using your package manager and try again.
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
# 运行服务器
./sling serve -l 0.0.0.0:5451 -l [::]:5451 -a 127.0.0.1:5453 -b https://nat-test-1.walterlv.com -p 50000-51000
```
