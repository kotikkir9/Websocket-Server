using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Websocket_Server.src
{
    public class Worker
    {
        public Guid Id { get; }

        private TcpClient client;
        private Server server;

        public Worker(TcpClient client, Server server)
        {
            this.client = client;
            this.server = server;
            this.Id = Guid.NewGuid();
            
            Thread thread = new Thread(this.Run);
            thread.Start();
        }

        public void Run()
        {
            Console.WriteLine("[{0}] - Client has connected.", Id);
            NetworkStream stream = client.GetStream();

            // enter to an infinite cycle to be able to handle every change in stream
            while (true)
            {
                // while (!stream.DataAvailable);
                // while (client.Available < 3); // match against "get"

                byte[] bytes = new byte[client.ReceiveBufferSize];
                stream.Read(bytes, 0, bytes.Length);
                string s = Encoding.UTF8.GetString(bytes);

                if (Regex.IsMatch(s, "^GET", RegexOptions.IgnoreCase))
                {
                    // Console.Write("[Worker {0}] - |===== Handshaking from client =====|\n{1}", Id, s);

                    // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                    // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                    // 3. Compute SHA-1 and Base64 hash of the new value
                    // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
                    string swk = Regex.Match(s, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                    string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                    byte[] swkaSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                    string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);

                    // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
                    byte[] response = Encoding.UTF8.GetBytes(
                        "HTTP/1.1 101 Switching Protocols\r\n" +
                        "Connection: Upgrade\r\n" +
                        "Upgrade: websocket\r\n" +
                        "Sec-WebSocket-Accept: " + swkaSha1Base64 + "\r\n\r\n");

                    stream.Write(response, 0, response.Length);
                    server.AddWorker(this);
                    server.Broadcast($"New client connected.", this);
                }
                else
                {
                    bool fin = (bytes[0] & 0b10000000) != 0,
                        mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"
                    int opcode = bytes[0] & 0b00001111, // expecting 1 - text message
                        offset = 2;
                    ulong msglen = (ulong)bytes[1] & 0b01111111;
                    System.Console.WriteLine("[{4}] - fin: {0}, mask: {1}, opcode: {2}, msglen: {3}", fin, mask, opcode, msglen, Id);

                    if (opcode == 8)
                    {
                        server.RemoveWorker(this);
                        server.Broadcast("Client has disconnected.", this);
                        Console.WriteLine("[{0}] - Client disconnected!", Id);
                        client.Close();
                        break;
                    }

                    if (msglen == 126)
                    {
                        // bytes are reversed because websocket will print them in Big-Endian, whereas
                        // BitConverter will want them arranged in little-endian on windows
                        msglen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                        offset = 4;
                    }
                    else if (msglen == 127)
                    {
                        // To test the below code, we need to manually buffer larger messages ??? since the NIC's autobuffering 
                        // may be too latency-friendly for this code to run (that is, we may have only some of the bytes in this
                        // websocket frame available through client.Available).  
                        System.Console.WriteLine("[{0}] - 127 HERE", Id);
                        msglen = BitConverter.ToUInt64(new byte[] { bytes[9], bytes[8], bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2] });
                        offset = 10;
                    }

                    if (msglen == 0)
                        Console.WriteLine("[{0}] - msglen == 0");
                    else if (mask)
                    {
                        byte[] decoded = new byte[msglen];
                        byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
                        offset += 4;

                        for (ulong i = 0; i < msglen; ++i)
                        {
                            decoded[i] = (byte)(bytes[(ulong)offset + i] ^ masks[i % 4]);
                        }

                        string text = Encoding.UTF8.GetString(decoded);
                        Console.WriteLine("[{0}] - Received message: {1}", Id, text);

                        server.Broadcast(text, this);
                    }
                    else
                        Console.WriteLine("[{0}] - mask bit not set", Id);

                    Console.WriteLine();
                }
            }

            System.Console.WriteLine("[{0}] - Terminated.\n", Id);
        }


        public void SendMessage(string message)
        {
            List<byte> response = new List<byte>();
            response.Add((byte)0b_1000_0001);                                                   // opcode 0x81 - text message UTF-8

            // First bit of the following byte is 0, which means that the message is NOT masked
            // RFC 6455 - Section 5.1:
            // A server MUST NOT mask any frames that it sends to the client. A client MUST close a connection if it detects a masked frame.
            if (message.Length < 126)
            {
                byte len = (byte)((byte)message.Length);
                response.Add(len);

            }
            else if (message.Length < 65536)
            {
                response.Add(0b_0111_1110);                                                     // 126 => length >= 126 && length < 65536
                response.AddRange(BitConverter.GetBytes((UInt16)(message.Length)).Reverse());   // 2 bytes that represent the length of the message
            }
            else
            {
                //NOT TESTED!
                response.Add(0b_0111_1111);                                                     // 127 => length >= 65536
                response.AddRange(BitConverter.GetBytes((UInt64)(message.Length)).Reverse());   // 8 bytes that represent the length of the message 
            }

            foreach(byte b in message) response.Add(b);                                         // message itself as byte array
            client.GetStream().Write(response.ToArray(), 0, response.Count);                                // convert list to array, and send to client
        }
    }
}