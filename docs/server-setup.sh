#!/bin/bash
# ============================================================
# gm.sodasa.org 伺服器初始化腳本（只需執行一次）
# 在 Linux 伺服器以 root 或 sudo 執行
# ============================================================

set -e

DEPLOY_DIR="/opt/sodagm"
SERVICE_NAME="sodagm"
SERVICE_USER="www-data"   # 或改成你的用戶名

echo "=== 安裝 .NET 6 執行環境 ==="
# Ubuntu/Debian
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
dpkg -i /tmp/packages-microsoft-prod.deb
apt-get update
apt-get install -y aspnetcore-runtime-6.0

echo "=== 建立部署目錄 ==="
mkdir -p "$DEPLOY_DIR"
chown -R "$SERVICE_USER":"$SERVICE_USER" "$DEPLOY_DIR"

echo "=== 建立 systemd 服務 ==="
cat > /etc/systemd/system/${SERVICE_NAME}.service << EOF
[Unit]
Description=Soda GM Tool API
After=network.target

[Service]
Type=simple
User=$SERVICE_USER
WorkingDirectory=$DEPLOY_DIR
ExecStart=/usr/bin/dotnet $DEPLOY_DIR/WebApi.dll
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

# 允許 systemctl restart 不需要密碼（給 GitHub Actions SSH 用）
[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable "$SERVICE_NAME"

echo "=== 設定 sudoers（讓部署用戶可 restart 服務）==="
echo "$SERVICE_USER ALL=(ALL) NOPASSWD: /bin/systemctl restart $SERVICE_NAME" >> /etc/sudoers.d/sodagm
chmod 440 /etc/sudoers.d/sodagm

echo "=== 設定 nginx 反向代理 ==="
cat > /etc/nginx/sites-available/sodagm << 'NGINX'
server {
    listen 80;
    server_name gm.sodasa.org;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    server_name gm.sodasa.org;

    ssl_certificate     /etc/letsencrypt/live/gm.sodasa.org/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/gm.sodasa.org/privkey.pem;

    location / {
        proxy_pass         http://127.0.0.1:5050;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
NGINX

ln -sf /etc/nginx/sites-available/sodagm /etc/nginx/sites-enabled/
nginx -t && systemctl reload nginx

echo "=== 產生 SSH Key 給 GitHub Actions 使用 ==="
ssh-keygen -t ed25519 -C "github-actions-deploy" -f /tmp/deploy_key -N ""
echo ""
echo "============================================================"
echo "  請將以下 PUBLIC KEY 加入伺服器 authorized_keys："
echo "============================================================"
cat /tmp/deploy_key.pub
echo ""
echo "  執行：cat /tmp/deploy_key.pub >> ~/.ssh/authorized_keys"
echo ""
echo "============================================================"
echo "  請將以下 PRIVATE KEY 加入 GitHub Secrets (SERVER_SSH_KEY)："
echo "============================================================"
cat /tmp/deploy_key
echo ""
echo "  設定完請刪除：rm /tmp/deploy_key /tmp/deploy_key.pub"
echo "============================================================"
echo ""
echo "=== 完成！還需要在 GitHub 設定以下 Secrets ==="
echo "  SERVER_HOST   = 你的伺服器 IP 或 sodasa.org"
echo "  SERVER_USER   = $SERVICE_USER"
echo "  SERVER_SSH_KEY= (上面產生的 private key 內容)"
echo "  SERVER_PORT   = 22 (預設可不設)"
echo "  DEPLOY_DIR    = $DEPLOY_DIR"
echo "  SERVICE_NAME  = $SERVICE_NAME"
