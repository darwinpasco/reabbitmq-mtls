# RabbitMQ mTLS with .NET Clients (Sender & Receiver)

This repository demonstrates how to set up **mutual TLS (mTLS)** with RabbitMQ running in Docker, and how to connect .NET sender and receiver applications using client certificates for authentication.

---

## 📦 Features

* RabbitMQ running in Docker with TLS (port 5671)
* Enforced mutual TLS (`verify_peer`, `fail_if_no_peer_cert = true`)
* EXTERNAL authentication (RabbitMQ user mapped from client certificate CN)
* Certificate generation script (`generate-mtls-certs.ps1`)
* Example .NET sender and receiver apps using mTLS

---

## ⚙️ Requirements

* Windows 10/11 with Docker Desktop
* PowerShell
* OpenSSL installed (from Git Bash or Win64 OpenSSL)
* .NET 6+ SDK

---

## 🚀 Quick Start

### 1. Clone repository

```bash
git clone https://github.com/<your-username>/rabbitmq-mtls.git
cd rabbitmq-mtls
```

### 2. Generate certificates

Run the PowerShell script:

```powershell
./generate-mtls-certs.ps1
```

This creates a `certs/` directory with:

* `ca.crt`, `ca.key` (Certificate Authority)
* `server.crt`, `server.key` (for RabbitMQ)
* `client.pfx` (for .NET apps)

### 3. Configure RabbitMQ

RabbitMQ is configured via `rabbitmq.conf`:

```ini
listeners.ssl.default = 5671
ssl_options.cacertfile = /etc/rabbitmq/certs/ca.crt
ssl_options.certfile   = /etc/rabbitmq/certs/server.crt
ssl_options.keyfile    = /etc/rabbitmq/certs/server.key
ssl_options.verify     = verify_peer
ssl_options.fail_if_no_peer_cert = true
auth_mechanisms.1 = EXTERNAL
ssl_cert_login_from = common_name
```

### 4. Start RabbitMQ

```powershell
docker compose up -d
```

### 5. Create RabbitMQ users

RabbitMQ maps client cert CN → username. If your client cert CN = `sender-app`, create the user:

```powershell
docker exec -it rabbitmq-mtls rabbitmqctl add_user sender-app ""
docker exec -it rabbitmq-mtls rabbitmqctl set_permissions -p / sender-app ".*" ".*" ".*"
```

### 6. Run the .NET sender

```powershell
cd src/Sender
dotnet run
```

This publishes messages over mTLS to RabbitMQ.

### 7. Run the .NET receiver

```powershell
cd src/Receiver
dotnet run
```

This consumes messages securely over mTLS.

---

## 📂 Repository Structure

```
.
├── certs/                  # Generated certificates (not committed)
├── docker-compose.yml      # RabbitMQ container setup
├── rabbitmq.conf           # RabbitMQ TLS + EXTERNAL config
├── generate-mtls-certs.ps1 # Certificate generation script
├── src/
│   ├── Sender/             # .NET Sender app
│   └── Receiver/           # .NET Receiver app
└── README.md               # This file
```

---

## 🔐 How mTLS works here

* The client presents a certificate (`client.pfx`) to RabbitMQ.
* RabbitMQ validates it against the CA (`ca.crt`).
* RabbitMQ maps the certificate CN (`sender-app`) to a RabbitMQ user.
* The user must exist in RabbitMQ with appropriate permissions.
* Both sender and receiver apps connect only via TLS (5671).

---

## 🧪 Testing with OpenSSL

Check server TLS:

```powershell
openssl s_client -connect localhost:5671 -CAfile certs/ca.crt -servername localhost
```

Check mutual TLS:

```powershell
openssl s_client -connect localhost:5671 `
  -CAfile certs/ca.crt `
  -cert certs/client.crt `
  -key certs/client.key `
  -servername localhost
```

---

## ⚠️ Notes

* Do not copy `ca.key` into the container. It stays private on the host.
* For multiple apps, generate unique client certs with different CNs and create corresponding RabbitMQ users.
* For production, protect private keys and PFX with strong passwords.

---

## 📜 License

MIT
