// https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server#put_together

using System.Net;
using System.Net.Sockets;
namespace Websocket_Server.src
{
    public class Server
    {
        private static int workerId = 0;

        private string ip = "127.0.0.1";
        private int port = 80;
        private TcpListener server;
        private TcpClient? client;

        private IList<Worker> workers;

        public Server()
        {
            server = new TcpListener(IPAddress.Parse(ip), port);
            workers = new List<Worker>();
        }

        public void start()
        {
            server.Start();
            Console.WriteLine("[Server] - Server has started on {0}:{1}\n", ip, port);

            while (true)
            {
                try
                {
                    client = server.AcceptTcpClient();
                   
                    Worker worker = new Worker(client, this, ++workerId);
                    workers.Add(worker);

                    Thread thread = new Thread(worker.Run);
                    thread.Start(); 

                    client = null;
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("[Server] - Something went wrong while accepting a client: {0}", e.Message);
                    // return;
                }
            }
        }

        public void removeWorker(Worker worker)
        {
            workers.Remove(worker);
            System.Console.WriteLine("[Server] - Worker {0} has been removed.", worker.Id);
        }
    }
}