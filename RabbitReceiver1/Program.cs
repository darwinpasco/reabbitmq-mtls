using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

class Receiver
{
    static void Main(string[] args)
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost",      // must match SAN in server.crt
            Port = 5671,                 // TLS port
            Ssl = new SslOption
            {
                Enabled = true,
                Version = SslProtocols.Tls12,
                ServerName = "localhost", // must match SAN in server.crt
                Certs = new X509Certificate2Collection(
                    new X509Certificate2(@"D:\DockerCompose\RabbitMQ\certs\client.pfx", "changeit")
                )
            },
            AuthMechanisms = new IAuthMechanismFactory[]
            {
                new ExternalMechanismFactory()
            },
            UserName = "", // ignored with EXTERNAL
            Password = ""
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        string exchangeName = "DemoExchange";
        string routingKey = "demo-routing-key";
        string queueName = "DemoQueue";

        channel.ExchangeDeclare(exchange: exchangeName, type: ExchangeType.Direct, durable: true);
        channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(queue: queueName, exchange: exchangeName, routingKey: routingKey);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (model, ea) =>
        {
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
            
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            Console.WriteLine($" [x] Received: {message}");
        };

        channel.BasicConsume(queue: queueName, autoAck: true, consumer: consumer);

        Console.WriteLine("Receiver running. Press [enter] to exit.");
        Console.ReadLine();
    }
}
