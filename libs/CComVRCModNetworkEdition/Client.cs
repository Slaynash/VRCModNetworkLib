using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace CCom
{
    internal class Client
    {
        private static TcpClient socket;
        private static SslStream sslStream;


        private Thread thread;
        private Thread keepaliveThread;

        private bool listen = true;


        internal bool autoReconnect = false;

        private string address;
        private int port;
        private string clientVersion = "0.0";

        private StreamReader inputStream;

        private IConnectionListener connectionListener;

        public Client(string address, int port, string clientVersion)
        {
            this.address = address;
            this.port = port;
            this.clientVersion = clientVersion;

            keepaliveThread = new Thread(() =>
            {
                while (true)
                {

                    if (listen)
                    {
                        try
                        {
                            WriteLine("KEEPALIVE");
                        }
                        catch(Exception e)
                        {
                            MelonModLogger.LogError("Error while trying to send keepalive: " + e);
                        }
                    }
                    Thread.Sleep(3000);
                }
            });
            keepaliveThread.Name = "VRCMod Networking Thread (Keepalive)";
            keepaliveThread.IsBackground = true;
        }

        public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private void ClientThread()
        {
            try
            {
                if (connectionListener != null) connectionListener.ConnectionStarted();
                MelonModLogger.Log("Connecting to server...");
                socket = new TcpClient();
                socket.ReceiveTimeout = 4000; //4s
                socket.Connect(address, port);
                sslStream = new SslStream(
                    socket.GetStream(),
                    false,
                    new RemoteCertificateValidationCallback(ValidateServerCertificate),
                    null
                );
                sslStream.AuthenticateAsClient(address);
                string ln;

                inputStream = new StreamReader(sslStream);
                MelonModLogger.Log("Waiting for connection...");
                if (connectionListener != null) connectionListener.WaitingForConnection();
                while ((ln = inputStream.ReadLine()) != null && ln != "READY");

                MelonModLogger.Log("Connecting...");
                if (connectionListener != null) connectionListener.Connecting();
                WriteLine("VRCMODNW_" + clientVersion);
                if ((ln = ReadLine()) == null || !ln.Equals("OK"))
                {
                    throw new Exception("Connection aborted");
                }

                if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsPlayer)
                socket.ReceiveTimeout = -1; //no receive timeout

                MelonModLogger.Log("Connected.");
                if (connectionListener != null) connectionListener.Connected();
            }
            catch (Exception e)
            {
                MelonModLogger.LogError("Unable to connect: " + e);
                if (connectionListener != null) connectionListener.ConnectionFailed(e.ToString());
                return;
            }
            try
            {
                socket.ReceiveTimeout = 7000;
                listen = true;
                if(!keepaliveThread.IsAlive)
                    keepaliveThread.Start();
                Listen();
                if (connectionListener != null) connectionListener.Disconnected("Connection closed");
            }
            catch (Exception e)
            {
                MelonModLogger.LogError("Disconnected from server: " + e);
                if (connectionListener != null) connectionListener.Disconnected(e.ToString());
            }
        }


        public void StartConnection()
        {
            if (thread != null && thread.IsAlive)
            {
                MelonModLogger.LogError("Unable to start connection: The connection thread is already started");
                return;
            }
            thread = new Thread(() =>
            {
                ClientThread();
                while (autoReconnect)
                {
                    Thread.Sleep(15000);
                    ClientThread();
                }
            });
            thread.Name = "VRCMod Networking Thread (Listen)";
            thread.IsBackground = true;
            thread.Start();
        }

        public string ReadLine()
        {
            string lin;
            while ((lin = inputStream.ReadLine()) != null)
            {
                if (lin.Equals("KEEPALIVE")) continue;
                else
                {
                    MelonModLogger.Log(" <<< " + lin);
                    return lin;
                }
            }
            return lin;
        }

        public string ReadLineSecure()
        {
            while (true)
            {
                string lin = inputStream.ReadLine();
                if (lin != null) MelonModLogger.Log(" <<< *****************");
                return lin;
            }
        }

        public void WriteLine(string lout)
        {
            if(!"KEEPALIVE".Equals(lout)) MelonModLogger.Log(" >>> " + lout);
            sslStream.Write(Encoding.UTF8.GetBytes(lout + "\r\n"));
        }

        public void WriteLineSecure(string lout)
        {
            MelonModLogger.Log(" >>> *****************");
            sslStream.Write(Encoding.UTF8.GetBytes(lout + "\r\n"));
        }

        public void WriteLineNoLog(string lout)
        {
            sslStream.Write(Encoding.UTF8.GetBytes(lout + "\r\n"));
        }

        private void Listen()
        {
            string input = "";
            while (listen && (input = ReadLine()) != null)
            {
                CommandManager.RunCommand(input, this);
            }
            listen = false;
        }

        public void SetConnectionListener(IConnectionListener connectionEventListener)
        {
            connectionListener = connectionEventListener;
        }
    }
}
