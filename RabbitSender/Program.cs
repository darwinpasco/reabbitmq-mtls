using RabbitMQ.Client;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

static IConnection CreateTlsConnection()
{
    var factory = new ConnectionFactory
    {
        HostName = "localhost",
        Port = 5671,
        ClientProvidedName = "Rabbit Sender App",
        Ssl = new SslOption
        {
            Enabled = true,
            Version = SslProtocols.Tls12,
            ServerName = "localhost", // must match SAN in server.crt
            Certs = new X509Certificate2Collection(
                new X509Certificate2(@"D:\DockerCompose\RabbitMQ\certs\client.pfx", "changeit")
            ),
            // Be strict in production. Keep default validation. No permissive callbacks.
        },
        AuthMechanisms = new IAuthMechanismFactory[] { new ExternalMechanismFactory() },
        //UserName = "sender-app",
        //Password = "sender-app"
    };

    return factory.CreateConnection();
}


using var conn = CreateTlsConnection();
using var ch = conn.CreateModel();

string ex = "DemoExchange";
string rk = "demo-routing-key";
string q = "DemoQueue";


ch.ExchangeDeclare(ex, ExchangeType.Direct, durable: true, autoDelete: false);
ch.QueueDeclare(q, durable: true, exclusive: false, autoDelete: false);
ch.QueueBind(q, ex, rk);

for (int i = 0; i < 60; i++)
{
    Console.WriteLine($"Sending Message {i}");
    byte[] body = Encoding.UTF8.GetBytes($"Message #{i}");
    ch.BasicPublish(ex, rk, basicProperties: null, body);
    Thread.Sleep(1000);
}

ch.Close();

