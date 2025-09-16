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
using var channel = conn.CreateModel();


string exchangeName = "DemoExchange";
string routingKey = "demo-routing-key";
string queueName = "DemoQueue";



channel.ExchangeDeclare(exchangeName, ExchangeType.Direct, durable: true, autoDelete: false);
channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
channel.QueueBind(queueName, exchangeName, routingKey);

for (int i = 0; i < 60; i++)
{
    Console.WriteLine($"Sending Message {i}");
    byte[] body = Encoding.UTF8.GetBytes($"Message #{i}");
    channel.BasicPublish(exchangeName, routingKey, basicProperties: null, body);
    Thread.Sleep(1000);
}

channel.Close();

