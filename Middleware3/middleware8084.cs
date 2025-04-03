using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Drawing;

public class Program
{
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Form1());
    }
}

public class Form1 : Form
{
    private Button sendButton;
    private ListView sentList, receivedList, readyList;
    private TcpListener listener;
    private int messageCounter = 1;
    private SortedDictionary<int, string> deliveryQueue = new SortedDictionary<int, string>();
    private int nextExpectedSequence = 1;
    private readonly object queueLock = new object();
    private HashSet<string> processedMessages = new HashSet<string>();
    private readonly object processedLock = new object();

    public Form1()
    {
        this.Text = "Middleware 3";
        this.ClientSize = new Size(1000, 400);
        InitializeGUI();
        listener = new TcpListener(IPAddress.Any, 8084);
        listener.Start();
        ListenForClientsAsync();
    }

    private void InitializeGUI()
    {
        sendButton = new Button
        {
            Text = "Send",
            Size = new Size(75, 30),
            Location = new Point(35, 15)
        };
        sendButton.Click += SendMessageHandler;
        sentList = CreateListView("Sent Messages", 35, 50);
        receivedList = CreateListView("Received Messages", 385, 50);
        readyList = CreateListView("Ready Messages", 735, 50);
        Controls.Add(sendButton);
        Controls.Add(sentList);
        Controls.Add(receivedList);
        Controls.Add(readyList);
    }

    private ListView CreateListView(string title, int x, int y)
    {
        var listView = new ListView
        {
            View = View.Details,
            Location = new Point(x, y),
            Size = new Size(300, 200),
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            Scrollable = true
        };
        var columnHeader = new ColumnHeader { Text = title };
        listView.Columns.Add(columnHeader);
        listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        return listView;
    }

    private async void SendMessageHandler(object sender, EventArgs e)
    {
        string messageId = $"Msg#{messageCounter++} from {this.Text} at {DateTime.Now:HH:mm:ss}";
        lock (sentList)
            sentList.Items.Add(messageId);
        using (TcpClient networkClient = new TcpClient("localhost", 8081))
        {
            byte[] data = Encoding.UTF8.GetBytes(messageId);
            await networkClient.GetStream().WriteAsync(data, 0, data.Length);
        }
    }

    private async void ListenForClientsAsync()
    {
        while (true)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() => ProcessClient(client));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Listener error: {ex.Message}");
            }
        }
    }

    private async Task ProcessClient(TcpClient client)
    {
        try
        {
            using (client)
            {
                byte[] buffer = new byte[1024];
                int bytesRead = await client.GetStream().ReadAsync(buffer, 0, buffer.Length);
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim('\0');
                
                if (!message.StartsWith("SEQ:") && !message.StartsWith("SEQREQ:"))
                {
                    string coreMessage = ExtractCoreMessage(message);
                    lock (processedLock)
                    {
                        if (!processedMessages.Contains(coreMessage))
                        {
                            processedMessages.Add(coreMessage);
                            receivedList.Invoke((MethodInvoker)(() =>
                                receivedList.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}")));
                        }
                    }
                }
                
                if (message.StartsWith("SEQ:"))
                    ProcessSequenceMessage(message);
                else if (!message.StartsWith("SEQREQ:"))
                    await RequestSequenceAssignment(message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client processing error: {ex.Message}");
        }
    }

    private async Task RequestSequenceAssignment(string message)
    {
        using (TcpClient seqClient = new TcpClient("localhost", 8082))
        {
            string request = $"SEQREQ:{message}:{DateTime.UtcNow.Ticks}";
            byte[] data = Encoding.UTF8.GetBytes(request);
            await seqClient.GetStream().WriteAsync(data, 0, data.Length);
        }
    }

    private void ProcessSequenceMessage(string message)
    {
        var parts = message.Split(':');
        if (parts.Length < 3) return;
        
        int sequenceNumber;
        if (!int.TryParse(parts[1], out sequenceNumber))
            return;
            
        string originalMessage = parts[2];
        string coreMessage = ExtractCoreMessage(originalMessage);
        
        // Ensure the message is in the Received list
        lock (processedLock)
        {
            if (!processedMessages.Contains(coreMessage))
            {
                processedMessages.Add(coreMessage);
                receivedList.Invoke((MethodInvoker)(() =>
                    receivedList.Items.Add($"[{DateTime.Now:HH:mm:ss}] {originalMessage}")));
            }
        }
        
        lock (queueLock)
        {
            deliveryQueue[sequenceNumber] = originalMessage;
            
            while (deliveryQueue.ContainsKey(nextExpectedSequence))
            {
                readyList.Invoke((MethodInvoker)(() =>
                    readyList.Items.Add($"[SEQ {nextExpectedSequence}] {deliveryQueue[nextExpectedSequence]}")));
                deliveryQueue.Remove(nextExpectedSequence);
                nextExpectedSequence++;
            }
        }
    }

    private string ExtractCoreMessage(string message)
    {
        if (message.Contains("Msg#") && message.Contains("from Middleware"))
        {
            int startIndex = message.IndexOf("Msg#");
            int endIndex = message.IndexOf(" at ");
            if (endIndex > startIndex)
                return message.Substring(startIndex, endIndex - startIndex).Trim();
        }
        return message;
    }
}
