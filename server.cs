using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace ServerSide
{
    internal class Program
    {
        private static List<Client> connectedClients = new List<Client>();
        private static TcpListener listener;
        static void Main(string[] args)
        {
            //get ip address to host server
            Console.Write("Enter the ip address to serve at: ");
            IPAddress serverip = IPAddress.Parse(Console.ReadLine());

            //get port to host server
            Console.Write("Enter the port to listen at: ");
            int port = Convert.ToInt32(Console.ReadLine());

            //start listener
            listener = new TcpListener(serverip, port);
            listener.Start();
            Console.WriteLine("Server started.");

            //accept clients continously
            new Thread(() =>
            {
                while (true)
                {
                    Socket client = listener.AcceptSocket();
                    string[] clientinfo = PromptLoginInfo(client);
                    string username = clientinfo[0];
                    string password = clientinfo[1];
                    int file_user_id = 0;
                    string file_username = "";
                    string file_password = "";
                    string file_privileges = "user";
                    bool file_suspended = false;
                    bool matchfound = false;
                    string[] lines = System.IO.File.ReadAllLines(@"C:\Users\baa2h\Documents\_MyDocuments\Kodlama\C#\ChatRoom2\ServerSide\ServerSide\ServerSide\bin\Debug\database.txt");
                    foreach (string line in lines)
                    {
                        string[] fileinfo = line.Split(' ');
                        file_user_id = Convert.ToInt32(fileinfo[0]);
                        file_username = fileinfo[1];
                        file_password = fileinfo[2];
                        file_privileges = fileinfo[3];
                        file_suspended = fileinfo[4] == "yes";
                        if(username == file_username && password == file_password)
                        {
                            matchfound = true;
                            break;
                        }
                    }
                    NetworkStream stream = new NetworkStream(client);
                    if (!matchfound) 
                    {
                        Console.WriteLine("Failed login attempt with username : {0} and password : {1}", username, password);
                        byte[] errorBytes = Encoding.ASCII.GetBytes("Invalid username or password");
                        stream.Write(errorBytes, 0, errorBytes.Length);
                        client.Close();
                    }
                    else
                    {
                        if (file_suspended)
                        {
                            Console.WriteLine("Failed login attempt (suspended) with username : {0} and password : {1}", username, password);
                            byte[] errorBytes = Encoding.ASCII.GetBytes("Your account is suspended, contact the admin of the server");
                            stream.Write(errorBytes, 0, errorBytes.Length);
                            client.Close();
                        }
                        else
                        {
                            bool canLogin = true;
                            if (connectedClients.Count != 0)
                            {
                                Console.WriteLine("user {0} is attempting to login.... Clients already connected are:",username);
                                foreach (Client client1 in connectedClients)
                                {
                                    Console.WriteLine(client1.username);
                                    if (client1.username == username)
                                    {
                                        lineChanger(file_user_id.ToString() + " " + file_username + " " + file_password + " " + file_privileges + " " + "yes", @"C:\Users\baa2h\Documents\_MyDocuments\Kodlama\C#\ChatRoom2\ServerSide\ServerSide\ServerSide\bin\Debug\database.txt", file_user_id);
                                        byte[] errorBytes = Encoding.ASCII.GetBytes("Your account has been suspended, contact the admin of the server");
                                        stream.Write(errorBytes, 0, errorBytes.Length);
                                        client.Close();
                                        canLogin = false;
                                        Console.WriteLine("Account {0} was suspended",username);
                                        int j = 0;
                                        Client cl = null;
                                        for (int i = 0; i < connectedClients.Count; i++)
                                        {
                                            if (connectedClients[i].username == username)
                                            {
                                                cl = connectedClients[i];
                                                j = i;
                                                break;
                                            }
                                        }
                                        connectedClients[j].socket.Close();
                                        Console.WriteLine("User {0} disconnected: {1}", username, cl.ip_address);
                                        connectedClients.RemoveAt(j);
                                        break;

                                    }
                                }
                            }
                            if (canLogin)
                            {
                                byte[] welcomeBytes = Encoding.ASCII.GetBytes("Welcome to the chatroom, " + username + "!\n");
                                stream.Write(welcomeBytes, 0, welcomeBytes.Length);
                                connectedClients.Add(new Client(client,client.RemoteEndPoint,username));
                                Console.WriteLine("User : {0} logged in from {1}",username,client.RemoteEndPoint);
                                new Thread(() => { HandleClient(new Client(client, client.RemoteEndPoint, username)); }).Start();
                            }
                        }
                    }
                    
                }
            }).Start();
        }
        static void lineChanger(string newText, string fileName, int line_to_edit)
        {
            string[] arrLine = File.ReadAllLines(fileName);
            arrLine[line_to_edit - 1] = newText;
            File.WriteAllLines(fileName, arrLine);
        }
        private static string[] PromptLoginInfo(Socket client)
        {
            NetworkStream stream = new NetworkStream(client);

            byte[] usernamePrompt = Encoding.ASCII.GetBytes("Enter your username: ");
            stream.Write(usernamePrompt, 0, usernamePrompt.Length);
            byte[] usernameBytes = new byte[1024];
            int usernameBytesRead = stream.Read(usernameBytes, 0, usernameBytes.Length);
            string username = Encoding.ASCII.GetString(usernameBytes, 0, usernameBytesRead).Trim();

            byte[] passwordPrompt = Encoding.ASCII.GetBytes("Enter your password: ");
            stream.Write(passwordPrompt, 0, passwordPrompt.Length);
            byte[] passwordBytes = new byte[1024];
            int passwordBytesRead = stream.Read(passwordBytes, 0, passwordBytes.Length);
            string password = Encoding.ASCII.GetString(passwordBytes, 0, passwordBytesRead).Trim();

            return new string[] { username, password };
        }

        private static void HandleClient(Client client)
        {
            string username = client.username;
            try
            {
                NetworkStream stream = new NetworkStream(client.socket);
                while (true)
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if(bytesRead == 0)
                    {
                        break;
                    }
                    Console.WriteLine((username + ": " + System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead)));
                    foreach (Client other in connectedClients)
                    {
                        Socket otherSocket = other.socket;
                        string otherUsername = other.username;
                        if (other != client && otherSocket.Connected)
                        {
                            byte[] messageBytes = System.Text.Encoding.ASCII.GetBytes(username + ": " + System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead));
                            otherSocket.Send(messageBytes);
                            
                        }//move above line outside of foreach
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error handling client: {0}", e.Message);
            }
            finally
            {
                for (int i = 0;i<connectedClients.Count;i++)
                {
                    if (connectedClients[i].username == username)
                    {
                        connectedClients.RemoveAt(i);
                        break;
                    }
                }
                

                Console.WriteLine("User {0} disconnected: {1}", username, client.ip_address);

                // Close the client socket
                client.socket.Close();
            }
        }
    }

    class Client
    {
        public Socket socket;
        public EndPoint ip_address;
        public string username;
        public Client(Socket _socket,EndPoint _ip_address, string _username) 
        {
            socket = _socket;
            ip_address = _ip_address;
            username = _username;
        } 
    }
}
