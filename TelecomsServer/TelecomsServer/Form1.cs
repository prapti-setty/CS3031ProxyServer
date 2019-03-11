using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Threading;
using System.Net.Sockets;

namespace TelecomsServer
{

    //creating the form/console window that displays all our information
    //the window contains a start server button and a block url button, 
    //an input text box that can be used to block urls,
    //two log windows that display http and https requests
    public partial class Form1 : Form
    {
        public static string theHttpHosts = null;
        public static string theHttpsHosts = null;

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Thread startProg = new Thread(() => ProxyServer.MainProg());
            startProg.Start();
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            string block = textBox2.Text;
            ProxyServer.addBlockURL(block);
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            textBox1.Text = theHttpHosts;
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            textBox3.Text = theHttpsHosts;
        }

        public void changeHttps()
        {
            textBox1.Text = theHttpsHosts;
        }
    }

    public class ProxyServer
    {
        private TcpListener theListener;
        private bool runServer;
        public static List<string> blockedURL = new List<string>();

        //let the user know that the proxy server has been started
        public static void MainProg()
        {
            System.Console.WriteLine("Starting the Server");

            ProxyServer theServer = new ProxyServer();
            theServer.Start();
        }

        //the function called on when we block URLs
        public static void addBlockURL(string theURL)
        {
            blockedURL.Add(theURL);
        }
        //creating the start method that starts the listener
        public void Start()
        {
            this.theListener = new TcpListener(IPAddress.Any, 5000);
            this.theListener.Start();
            //ensure that we can run the server
            this.runServer = true;
            //execute these actions when our server is running
            while (this.runServer)
            {
                if (!theListener.Pending())
                {
                    //pause the thread for a small period
                    Thread.Sleep(200);
                    continue;
                }
                //create the tcp client for the listener
                TcpClient client = theListener.AcceptTcpClient();
                //create threads for the client session
                Thread session = new Thread(new ParameterizedThreadStart(ClientSession));
                session.Start(client);
            }
        }

        public void ClientSession(object client)
        {
            TcpClient clientTCP = (TcpClient)client;
            NetworkStream clientStream = clientTCP.GetStream();
            //create a buffer
            byte[] buffer = null;
            //continually perform this loop
            while (true)
            {
                buffer = null;
                if (clientStream.CanRead && clientStream != null)
                {
                    //save the request made in the buffer (as ascii)
                    buffer = NetworkManager.ReadMessage(clientStream);
                }
                else
                {
                    continue;
                }
                //get the string val of this and display to console
                string request = Encoding.ASCII.GetString(buffer);
                Console.WriteLine(request);
                string[] splitRequest = request.Split(new char[0]);
                string host = GetHostFromRequest(splitRequest);
                //if the host is blocked then we do not want to continue with the request
                if (!checkBlocked(host))
                {
                    Console.Write(host);
                    string httpOrHttps = IsHttpOrHttps(splitRequest);
                    //perform the action based on http or https
                    if (httpOrHttps == "HTTPS")
                    {
                        executeHttps(splitRequest, buffer, clientTCP, host);
                        Form1.theHttpsHosts += "\n" + host;
                        //Form1.changeHttps();
                    }
                    if (host == string.Empty)
                    {
                        continue;
                    }
                    Form1.theHttpHosts += "\n" + host;
                    //https port is 80 so create the tcpCkent for this and get network stream
                    TcpClient serverTCP = new TcpClient(host, 80);
                    NetworkStream serverStream = serverTCP.GetStream();
                    // Forward HTTP request to server
                    if (serverStream.CanWrite && serverStream != null)
                    {
                        NetworkManager.SendMessage(serverStream, buffer);
                    }
                    else
                    {
                        continue;
                    }
                    // Get HTTP response from server
                    if (serverStream.CanRead && serverStream != null)
                    {
                        buffer = NetworkManager.ReadMessage(serverStream);
                    }
                    else
                    {
                        continue;
                    }
                    string response = Encoding.ASCII.GetString(buffer);
                    Console.Write(response);

                    // Forward HTTP response to client
                    if (clientStream.CanWrite && clientStream != null)
                    {
                        NetworkManager.SendMessage(clientStream, buffer);
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    
                }
            }
        }

        //function to check if a url has been blocked. 
        //called on before the request is processed
        public bool checkBlocked(string host)
        {
            for (int i = 0; i < blockedURL.Count(); i++)
            {
                if (blockedURL[i].Contains(host))
                {
                    return true;
                }
            }
            return false;
        }

        //method to exectue https
        //very similar to http but we must be aware of the encryption
        //connection needs to be established first and 
        //we need to send a response for this connection
        public void executeHttps(string[] splitRequest, byte[] buffer, TcpClient client, string host)
        {
            NetworkStream clientStream = client.GetStream();
            if (host == string.Empty)
            {
                return;
            }
            //https come sin over port 443
            //create the tcp client for this and find the network stream for it
            TcpClient serverTCPhttps = new TcpClient(host, 443);
            NetworkStream serverStream = serverTCPhttps.GetStream();
            // Forward connection establish request
            byte[] establishConnection = Encoding.ASCII.GetBytes("HTTP/1.0 200 Connection established\r\n\r\n");
            NetworkManager.SendMessage(clientStream, establishConnection);
            //as it isencryped we just want to forward requests on from 443 to 5000
            int emptyRead1 = 0;
            int emptyRead2 = 0;
            while(emptyRead1 < 100 && emptyRead2 < 100)
            {
                byte[] temp1 = NetworkManager.ReadMessage(clientStream);
                if (temp1.Length != 0)
                {
                    NetworkManager.SendMessage(serverStream, temp1);
                    emptyRead1 = 0;
                }
                else
                {
                    emptyRead1++;
                }
                temp1 = NetworkManager.ReadMessage(serverStream);
                if (temp1.Length != 0)
                {
                    NetworkManager.SendMessage(clientStream, temp1);
                    emptyRead2 = 0;
                }
                else
                {
                    emptyRead2++;
                }
            }
            //close both client and server comunication if empty reads
            serverTCPhttps.Close();
            client.Close();
        }

        //function to check if it is a http or https request
        //https sends connect first
        //https can send get, post, etc
        public string IsHttpOrHttps(string[] request)
        {
            for (int i = 0; i < request.Length; i++)
            {
                if (request[i] == "CONNECT")
                {
                    return "HTTPS";
                }
                else
                    return "HTTP";
            }
            return string.Empty;
        }

        //used by both http and https
        //use this to get the host from the block of the request sent on
        //string matches host and returns what comes after host
        public string GetHostFromRequest(string[] request)
        {
            for (int i = 0; i < request.Length; i++)
            {
                if (request[i] == "Host:")
                {
                    string[] checkHost = request[i + 1].Split(':');

                    if (checkHost.Length != 1)
                    {
                        return checkHost[0];
                    }
                    else
                    {
                        return request[i + 1];
                    }
                }
            }

            return string.Empty;
        }
    }

    public class NetworkManager
    {

        //the function to read the message that is sent on from the network stream
        public static byte[] ReadMessage(NetworkStream stream)
        {
            byte[] receiveBuffer = new byte[8192];
            byte[] tempArray = new byte[0];
            byte[] returnBuffer = new byte[0];
            int receivedBytes = 0;
            stream.ReadTimeout = 3000;
            try
            {
                if (stream.CanRead && stream != null)
                {
                    while ((receivedBytes = stream.Read(receiveBuffer, 0, receiveBuffer.Length)) != 0)
                    {
                        returnBuffer = new byte[receivedBytes];
                        Array.Copy(receiveBuffer, 0, returnBuffer, 0, receivedBytes);
                        tempArray = tempArray.Concat(returnBuffer).ToArray();
                    }
                }
            }
            catch (IOException e)
            {

            }
            return tempArray;
        }

        //the function to send the message that is sent on from the network stream
        public static void SendMessage(NetworkStream stream, byte[] sendBuffer)
        {
            if (stream.CanWrite && stream != null)
            {
                stream.Write(sendBuffer, 0, sendBuffer.Length);
            }
        }
    }
}
