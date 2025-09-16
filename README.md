# RabbitMQ mTLS on Windows Docker: Complete Step by Step

---

## Prerequisites

* Windows 10 or 11 with Docker Desktop
* PowerShell
* OpenSSL in PATH (from Git for Windows or Win64 OpenSSL)
* .NET SDK for your C# apps

**Do everything in PowerShell. Do not use Git Bash.**

---

## 1) Folder layout

Create a working folder, for example:

```
D:\DockerCompose\RabbitMQ\
│  docker-compose.yml
│  rabbitmq.conf
└─ certs\   (generated in the next step)
```

Open PowerShell in `D:\DockerCompose\RabbitMQ`.

---

## 2) Generate certificates with one PowerShell script

Create `generate-mtls-certs.ps1` next to your compose file and run it.

```powershell
# generate-mtls-certs.ps1
$certDir = "certs"
if (!(Test-Path $certDir)) { New-Item -ItemType Directory -Path $certDir | Out-Null }
Set-Location $certDir

# 1) CA
openssl genrsa -out ca.key 4096
openssl req -x509 -new -nodes -key ca.key -sha256 -days 3650 `
  -out ca.crt -subj "/C=PH/O=Traxion/OU=Dev/CN=Traxion-Dev-CA"

# 2) Server cert with SANs for localhost, 127.0.0.1, rabbitmq
@"
[ req ]
default_bits       = 4096
prompt             = no
default_md         = sha256
req_extensions     = v3_req
distinguished_name = dn
[ dn ]
C  = PH
O  = Traxion
OU = Dev
CN = rabbitmq
[ v3_req ]
keyUsage = critical, digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = @alt_names
[ alt_names ]
DNS.1 = localhost
DNS.2 = rabbitmq
IP.1  = 127.0.0.1
"@ | Out-File -Encoding ascii server.cnf

openssl genrsa -out server.key 4096
openssl req -new -key server.key -out server.csr -config server.cnf
openssl x509 -req -in server.csr -CA ca.crt -CAkey ca.key -CAcreateserial `
  -out server.crt -days 825 -sha256 -extensions v3_req -extfile server.cnf

# 3) Client cert and PFX (CN becomes RabbitMQ username via EXTERNAL)
openssl genrsa -out client.key 4096
openssl req -new -key client.key -out client.csr -subj "/C=PH/O=Traxion/OU=Dev/CN=sender-app"
openssl x509 -req -in client.csr -CA ca.crt -CAkey ca.key -CAcreateserial `
  -out client.crt -days 825 -sha256
openssl pkcs12 -export -out client.pfx -inkey client.key -in client.crt -certfile ca.crt -passout pass:changeit

Write-Host "Done. Files in $(Get-Location)"
```

Run it:

```powershell
./generate-mtls-certs.ps1
```

You should now have `ca.crt`, `server.crt`, `server.key`, `client.pfx` inside `./certs`.

**Never put `ca.key` inside the RabbitMQ container.**

---

## 3) RabbitMQ configuration (TLS and EXTERNAL)

Create `rabbitmq.conf` next to your compose file:

```ini
# Listen on TLS only
listeners.ssl.default = 5671

# TLS files
ssl_options.cacertfile = /etc/rabbitmq/certs/ca.crt
ssl_options.certfile   = /etc/rabbitmq/certs/server.crt
ssl_options.keyfile    = /etc/rabbitmq/certs/server.key

# Enforce mutual TLS
ssl_options.verify = verify_peer
ssl_options.fail_if_no_peer_cert = true

# Use EXTERNAL authentication (client cert maps to username)
auth_mechanisms.1 = EXTERNAL

# Map username from certificate Common Name (CN)
ssl_cert_login_from = common_name

# Optional. Disable plaintext listener if you want TLS only
# listeners.tcp = none
```

Reason for `ssl_cert_login_from = common_name`: your client cert CN is `sender-app`. Without this, some setups try to match the whole DN.

---

## 4) Docker Compose

Create `docker-compose.yml`:

```yaml
version: "3.9"

services:
  rabbitmq:
    image: rabbitmq:3.13-management
    container_name: rabbitmq-mtls

    ports:
      - "5671:5671"    # AMQPS (TLS for clients)
      - "15672:15672"  # RabbitMQ Management UI
      # Uncomment if you also want non-TLS AMQP for testing
      # - "5672:5672"

    volumes:
      - ./certs:/etc/rabbitmq/certs:ro
      - ./rabbitmq.conf:/etc/rabbitmq/rabbitmq.conf:ro

    command: >
      bash -c "rabbitmq-plugins enable --offline rabbitmq_auth_mechanism_ssl &&
               rabbitmq-server"
```

Bring it up and verify ports:

```powershell
docker compose up -d
docker ps
```

You should see `0.0.0.0:5671->5671/tcp` and `0.0.0.0:15672->15672/tcp`.

Open UI at `http://localhost:15672` and later create an admin for convenience.

---

## 5) Create RabbitMQ users

EXTERNAL maps the client cert CN to a username. Your client cert CN is `sender-app`.

```powershell
# Create an admin for the UI
docker exec -it rabbitmq-mtls rabbitmqctl add_user admin admin
docker exec -it rabbitmq-mtls rabbitmqctl set_user_tags admin administrator

# Create the EXTERNAL user matching client CN
docker exec -it rabbitmq-mtls rabbitmqctl add_user sender-app ""
docker exec -it rabbitmq-mtls rabbitmqctl set_permissions -p / sender-app ".*" ".*" ".*"
```

Check:

```powershell
docker exec -it rabbitmq-mtls rabbitmqctl list_users
```


### 6. Create RabbitMQ users

RabbitMQ maps client cert CN → username. If your client cert CN = `sender-app`, create the user:

```powershell
docker exec -it rabbitmq-mtls rabbitmqctl add_user sender-app ""
docker exec -it rabbitmq-mtls rabbitmqctl set_permissions -p / sender-app ".*" ".*" ".*"
```

### 7. Run the .NET sender

```powershell
cd src/Sender
dotnet run
```

This publishes messages over mTLS to RabbitMQ.

### 8. Run the .NET receiver

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
