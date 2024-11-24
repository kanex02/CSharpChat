using System.Net.Sockets;
using System.Net;
using System;
using System.Text;
using System.Text.RegularExpressions;

class Server {
    public static void Main(string[] args) {
        TcpListener server = new TcpListener(IPAddress.Parse("127.0.0.1"), 8085);

        server.Start();
        Console.WriteLine("Server has started on 127.0.0.1:8085.");
        Console.WriteLine("Waiting for a connection...");

        TcpClient client = server.AcceptTcpClient();

        Console.WriteLine("A client connected.");

        NetworkStream networkStream = client.GetStream();

        while (true) {
            while (!networkStream.DataAvailable);

            while (client.Available < 3);

            byte[] bytes = new byte[client.Available];

            networkStream.Read(bytes, 0, bytes.Length);

            string data = Encoding.UTF8.GetString(bytes);

            if (Regex.IsMatch(data, "^GET")) {
                const string eol = "\r\n";

                string swk = Regex.Match(data, "Sec-Websocket-Key: (.*)").Groups[1].Value.Trim();

                string swkSalted = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

                byte[] swkSaltedSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(
                            Encoding.UTF8.GetBytes(
                                swkSalted
                            )
                        );

                string swkSaltedSha1Base64 = Convert.ToBase64String(swkSaltedSha1);

                byte[] response = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" + eol
                    + "Connection: Upgrade" + eol
                    + "Upgrade: websocket" + eol
                    + "Sec-Websocket-Accept: " + swkSaltedSha1Base64 + eol
                    + eol);

                networkStream.Write(response, 0, response.Length);
            } else {
                bool fin = (bytes[0] & 0b10000000) != 0,
                    mask = (bytes[1] & 0b10000000) != 0;
                    int opcode = bytes[0] & 0b00001111;
                    ulong offset = 2,
                        msglen = bytes[1] & (ulong)0b01111111;

                    if (msglen == 126) {
                        if (BitConverter.IsLittleEndian) {
                            msglen = BitConverter.ToUInt16([bytes[2], bytes[3]], 0);
                        } else {
                            msglen = BitConverter.ToUInt16([bytes[3], bytes[2]], 0);
                        }
                        
                        offset = 4;

                    } else if (msglen == 127) {
                        if (BitConverter.IsLittleEndian) {
                            msglen = BitConverter.ToUInt64([bytes[2], bytes[3], bytes[4], bytes[5], bytes[6], bytes[7], bytes[8], bytes[9]], 0);
                        } else {
                            msglen = BitConverter.ToUInt64([bytes[9], bytes[8], bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2]], 0);
                        }
                        offset = 10;
                    }

                    if (msglen == 0) {
                        Console.WriteLine("Empty message");
                        return;
                    }

                    if (!mask) {
                        Console.WriteLine("Mask bit not set");
                    }

                    byte[] decoded = new byte[msglen];
                    byte[] masks = [bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]];
                    offset += 4;
                    
                    for (ulong i = 0; i < msglen; ++i)
                        decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);

                    string text = Encoding.UTF8.GetString(decoded);
                    Console.WriteLine("{0}", text);

                    Console.WriteLine();
            } 
        }
    }
}