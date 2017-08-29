using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;
using System.IO;
using System.Xml;
using System.Runtime.InteropServices;

namespace Web38
{
    class Program
    {
        #region КОНСТАНТИ

        //Кількість робочих потоків
        static int _COUNT_WORK_THREAD = 3;

        //ІР основного сокета
        static string _IPSocketWork = "127.0.0.1";

        //Порт основного сокета
        static int _PortSocketWork = 5656;

        #endregion

        #region ГЛОБАЛЬНІ ЗМІННІ

        //Список підключених сокетів
        static List<Socket> _WorkSocketList = new List<Socket>();

        //Автоматика для регулюання потоків
        static AutoResetEvent _AutoResetEvent = new AutoResetEvent(false);

        //Признак запуску потоків
        static bool ThreadsRun = true;

        //Потік для прийому повідомлень
        static Thread threadListener = null;

        //Менеджер COM-соединений (COM connector)
        static V83.COMConnector V83COMConnector = null;

        //Строка підключення до 1С
        static string _1C_ConnectString = "";

        #endregion

        static void Main(string[] args)
        {
            Console.SetWindowSize(120, 50);

            Console.WriteLine("");
            for (int i = 0; i < 120; i++) Console.Write("-");
            Console.WriteLine("ВЕБ 3.8 для 1Cv8.3");
            Console.WriteLine("\n");
            Console.Write("Загрузка конфiгурацiї - [");
            GetConfigurateParameters();
            Console.Write("]");
            Console.WriteLine("\nРобочих потокiв - [" + _COUNT_WORK_THREAD.ToString() + "]");
            Console.WriteLine("Сервер - [" + _IPSocketWork + ":" + _PortSocketWork.ToString() + "]");
            Console.WriteLine("Пiдключення до сервера 1С - [" + _1C_ConnectString.ToString() + "]");
            for (int i = 0; i < 120; i++) Console.Write("-");
            Console.WriteLine("");

            //Менеджер COM-соединений (COM connector)
            V83COMConnector = new V83.COMConnector();
            V83COMConnector.PoolCapacity = 3;
            V83COMConnector.PoolTimeout = 10;
            //V83COMConnector.MaxConnections = 3;

            //Запуск основного потока для прийому повідомлень
            StartGeneralWorkerThread();

            //Запуск фонових потоків для обробки повідомлень
            CreateWorkerThread(_COUNT_WORK_THREAD);

            Console.ReadLine();
            Console.WriteLine("[" + DateTime.Now.ToString() + "] -> Close - <Start>");

            //Признак зупинки потоків
            ThreadsRun = false;

            Console.WriteLine("[" + DateTime.Now.ToString() + "] -> Close Listener - <Start>");

            //Закриваю потік для прийому повідомлень
            threadListener = null;
            
            Console.WriteLine("[" + DateTime.Now.ToString() + "] -> Close Listener - <OK>");
            Console.WriteLine("[" + DateTime.Now.ToString() + "] -> Close Workers - <Start>");

            //Активація робочих потоків і їх зупинка
            for (int k = 0; k < _COUNT_WORK_THREAD; k++)
                _AutoResetEvent.Set();

            Thread.Sleep(1000);

            Console.WriteLine("[" + DateTime.Now.ToString() + "] -> Close Workers - <OK>");
            Console.WriteLine("[" + DateTime.Now.ToString() + "] -> Close - <OK>");

            _AutoResetEvent = null;
            _WorkSocketList = null;

            //Очистка
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Marshal.ReleaseComObject(V83COMConnector);
            V83COMConnector = null;

            Console.WriteLine("Press any key ...");
            Console.ReadLine();
        }

        /// <summary>
        /// Функція запускає основний потік для прийому вхідних повідомлень
        /// </summary>
        static void StartGeneralWorkerThread()
        {
            Console.WriteLine();
            Console.WriteLine("[" + DateTime.Now.ToString() + "] --> Listener - <Create>");

            threadListener = new Thread(new ThreadStart(GeneralWorker));
            threadListener.IsBackground = true;
            threadListener.Start();

            Console.WriteLine("[" + DateTime.Now.ToString() + "] --> Listener [0] - <Start>");
        }

        /// <summary>
        /// Функція прийому вхідних повідомлень
        /// </summary>
        static void GeneralWorker()
        {
            //IP сервера на який будуть приходити повідомлення
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(_IPSocketWork), _PortSocketWork);

            //Створення основного сокета типу Stream на протоколі Tcp/IP
            Socket soketWork = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                Console.WriteLine("[" + DateTime.Now.ToString() + "] --> Listener [0][Connect] - <OK>");

                //Привязка до IP
                soketWork.Bind(localEndPoint);

                //Максимальна кількість підключень
                soketWork.Listen(1000);

                Console.WriteLine("[" + DateTime.Now.ToString() + "] --> Listener [0][Listen] - <OK>");

                while (ThreadsRun)
                {
                    Console.WriteLine("[" + DateTime.Now.ToString() + "] --> Listener [0][Accept] - <Accept>");

                    //Очікування підключення
                    Socket soketAccept = soketWork.Accept();
                    soketAccept.ReceiveTimeout = 1000;

                    Console.WriteLine("[" + DateTime.Now.ToString() + "] --> Listener [0][Accept][" + soketAccept.RemoteEndPoint.ToString() + "] - <OK>");

                    //Добавлення підключеного сокета в список для обробки на окремих потоках
                    lock (_WorkSocketList)
                        _WorkSocketList.Add(soketAccept);

                    //Команда СТАРТ обробки списку повідомлень для окремих потоків
                    _AutoResetEvent.Set();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[" + DateTime.Now.ToString() + "] --> Listener [0][Exception - <" + ex.Message + ">");
                ThreadsRun = false;
            }
            finally
            {
                //Закриваю підключення основного сокета
                soketWork.Close();
            }

            Console.WriteLine("[" + DateTime.Now.ToString() + "] <-- Listener [0] - <Close>");
        }

        /// <summary>
        /// Функція запускає фонові потоки для обробки повідомлень
        /// </summary>
        /// <param name="count">Кількість фонових потоків</param>
        static void CreateWorkerThread(int count)
        {
            Console.WriteLine("[" + DateTime.Now.ToString() + "] -> Workers - <Create>");

            for (int i = 0; i < count; i++)
            {
                if (ThreadsRun)
                {
                    Thread thread = new Thread(new ParameterizedThreadStart(Work));
                    thread.IsBackground = true;
                    thread.Start((object)i);

                    Console.WriteLine("[" + DateTime.Now.ToString() + "] -> Worker [" + i.ToString() + "] - <Start>");
                }
            }
        }

        /// <summary>
        /// Функція для обробки повідомлень
        /// </summary>
        /// <param name="p">Номер потоку</param>
        static void Work(object p)
        {
            object v83Base = null;

            try
            {
                //lock (V83COMConnector)
                    v83Base = V83COMConnector.Connect(_1C_ConnectString);

                Console.WriteLine("[{0}] -> Worker [{1}][Connect] - <OK>", DateTime.Now, p);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[{0}] -> Worker [{1}][Error] - {2}", DateTime.Now, p, ex.Message);
                return;
            }

            while (ThreadsRun)
            {
                //Сокет
                Socket soketAccept = null;

                //Перевірка списку підключених сокетів
                lock (_WorkSocketList)
                    if (_WorkSocketList.Count != 0)
                    {
                        Console.WriteLine("[{0}] -> Worker [{1}][Work] - <Message count {2}>", DateTime.Now, p, _WorkSocketList.Count);

                        soketAccept = _WorkSocketList[0];
                        _WorkSocketList.RemoveAt(0);
                    }

                //Обробка підключеного сокета
                if (soketAccept != null)
                {
                    //буфер для сокета
                    Byte[] buffer = new Byte[1024];

                    //Хмл-документ
                    XmlDocument xmlDoc = new XmlDocument();

                    //Назва функції в 1С та параметри функції
                    string server1cFunction = "";
                    object[] server1cFunctionPatams;

                    try
                    {
                        int receiveByte = 0;
                        string receiveXmlText = "";

                        //Зчитую дані
                        do
                        {
                            Console.WriteLine(soketAccept.Available);
                            receiveByte = soketAccept.Receive(buffer);
                            receiveXmlText += Encoding.GetEncoding(1251).GetString(buffer, 0, receiveByte);
                        }
                        while (soketAccept.Available > 0);

                        Console.WriteLine("[{0}] -> Worker [{1}][receiveXmlText] - {2}", DateTime.Now, p, receiveXmlText);

                        //Загрузка переданих хмл-даних
                        xmlDoc.LoadXml(receiveXmlText);

                        //Вітка з назвою процедури чи функції
                        XmlNode memberNameNode = xmlDoc.SelectSingleNode("/root/member");
                        if (memberNameNode != null)
                            server1cFunction = memberNameNode.InnerText;
                        else
                            throw new ArgumentNullException("Незадана назва функції/процедури");

                        Console.WriteLine("[{0}] -> Worker [{1}][Invoke] - <Member:{2}>", DateTime.Now, p, server1cFunction);

                        //Параметри сервера
                        XmlNodeList paramServerList = xmlDoc.SelectNodes("/root/server/item");
                        if (paramServerList != null)
                            for (int i = 0; i < paramServerList.Count; i++)
                                Console.WriteLine("[{0}] -> Worker [{1}][{2}] - <{3}>", DateTime.Now, p, paramServerList[i].Attributes["id"].Value, paramServerList[i].InnerText);

                        //Параметри процедури чи функції
                        XmlNodeList paramNameList = xmlDoc.SelectNodes("/root/params/item");
                        if (paramNameList != null)
                        {
                            server1cFunctionPatams = new object[paramNameList.Count];
                            for (int i = 0; i < paramNameList.Count; i++)
                            {
                                server1cFunctionPatams[i] = paramNameList[i].InnerText;
                                Console.WriteLine("[{0}] -> Worker [{1}] -> Param [{2}] = {3}", DateTime.Now, p, i, (paramNameList[i].InnerText.Length > 100 ? paramNameList[i].InnerText.Substring(0, 100) + " ... " : paramNameList[i].InnerText));
                            }
                        }
                        else
                            server1cFunctionPatams = null;

                        //Виклик функції/методу в 1с-ці
                        object returnValue = v83Base.GetType().InvokeMember(server1cFunction, BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null, v83Base, server1cFunctionPatams);

                        //Відправка результату
                        if (returnValue != null)
                            soketAccept.Send(Encoding.GetEncoding(1251).GetBytes(Convert.ToString(returnValue)));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[{0}] -> Worker [{1}][Exception] - <{2}>", DateTime.Now, p, ex.Message + "\n" + ex.StackTrace);
                        soketAccept.Send(Encoding.GetEncoding(1251).GetBytes(ex.Message + "\n" + ex.StackTrace));
                    }
                    finally
                    {
                        soketAccept.Close();
                    }
                }
                else
                {
                    Console.WriteLine("[{0}] -> Worker [{1}] - <Pause>", DateTime.Now, p);
                    _AutoResetEvent.WaitOne();
                }
            }

            v83Base = null;

            Console.WriteLine("[{0}] <- Worker [{1}] - <Close>", DateTime.Now, p);
        }

        /// <summary>
        /// Загрузка параметрів із конфігураційного файлу
        /// </summary>
        static void GetConfigurateParameters()
        {
            string configuration_file_path = AppDomain.CurrentDomain.BaseDirectory + "Web38.config";

            if (!File.Exists(configuration_file_path))
            {
                Console.Write("Помилка: Незнайдений конфігураційний файл " + configuration_file_path);
                return;
            }

            //Хмл-документ
            XmlDocument xmlDoc = new XmlDocument();

            try
            {
                xmlDoc.Load(configuration_file_path);
            }
            catch (Exception ex)
            {
                Console.Write("Помилка: " + ex.Message);
                return;
            }

            //Вітка з ІР адресою
            XmlNode nodeIPSocketWork = xmlDoc.SelectSingleNode("Web/IPSocketWork");
            if (nodeIPSocketWork != null)
                _IPSocketWork = nodeIPSocketWork.InnerText;

            //Вітка з Портом
            XmlNode nodePortSocketWork = xmlDoc.SelectSingleNode("Web/PortSocketWork");
            if (nodePortSocketWork != null)
                TryParseConfigurationParameters(nodePortSocketWork.InnerText, ref _PortSocketWork);

            //Вітка з кількітю робочих потоків
            XmlNode nodeCOUNT_WORK_THREAD = xmlDoc.SelectSingleNode("Web/COUNT_WORK_THREAD");
            if (nodeCOUNT_WORK_THREAD != null)
                TryParseConfigurationParameters(nodeCOUNT_WORK_THREAD.InnerText, ref _COUNT_WORK_THREAD);

            //Вітка з шляхом до бази даних 1С
            XmlNode node_1C_ConnectString = xmlDoc.SelectSingleNode("Web/_1C_ConnectString");
            if (node_1C_ConnectString != null)
                _1C_ConnectString = node_1C_ConnectString.InnerText;

            Console.Write("Ok");
        }

        /// <summary>
        /// Функція перетворює стрічкові параметри конфігураційного файлу в число,
        /// якщо не змогла перетворити, то встановлюється параметр по замовчуванню
        /// </summary>
        /// <param name="node_value">параметр з хмл файлу у вигляді innerText</param>
        /// <param name="parameter_value">конфігураційний параметр, у який записується перетворене значення</param>
        static void TryParseConfigurationParameters(string node_value, ref int parameter_value)
        {
            int _parameter;
            int.TryParse(node_value, out _parameter);
            if (_parameter > 0)
                parameter_value = _parameter;
        }
    }
}
