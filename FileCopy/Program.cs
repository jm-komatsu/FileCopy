using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.InteropServices;
using System.IO;    // 追加



namespace FileCopy
{
    class Program
    {
        private static Thread thServer;
        private static String ip = "127.0.0.1";
        private static int port = 9998;
        private static TcpClient client;
        private static TcpListener server;
        private static NetworkStream stream;
        private static string move_folder_path = "";
        private static string inifile_name = "flcopy.ini";
        static string sSave_log_Folder_Path = "";
        static string sSave_log_File_Path = "";
        static StreamWriter swWriter;
        static string sFolderIndex_Path = "";


        [DllImport("kernel32.dll")]
        static extern uint GetPrivateProfileString(
           string lpAppName,      // セクション名
           string lpKeyName,      // キー名
           string lpDefault,         // 規定の文字列 
           StringBuilder lpReturnedString,  // 情報が格納されるバッファ
           uint nSize,  // 情報バッファのサイズ
           string lpFileName  // .iniファイルの名前
        );

        [DllImport("kernel32.dll")]
        static extern uint WritePrivateProfileString(
            string lpAppName,
            string lpKeyName,
            string lpstring,
            string lpFileName
        );

        /* メイン */
        static void Main(string[] args)
        {
            Console.Title = "ファイル転送プログラム";
            Console.WriteLine("ファイル転送プログラム起動：{0}", DateTime.Now);

            /* ログファイル関係 */
            fnCreate_LogFolder(); // ログフォルダの作成、存在確認
            fnWrite_LogFile("INFO", "ファイル転送プログラムを起動しました。"); // ログファイル作成、存在確認

            /* Folderindex関係 */
            sFolderIndex_Path = string.Format("{0}\\{1}", System.IO.Directory.GetCurrentDirectory(), "folderindex.txt");   // IDリストのパス生成
            // IDリストファイルの存在確認
            if (System.IO.File.Exists(sFolderIndex_Path))
            {
            }
            else
            {
                Console.WriteLine(string.Format("警告：フォルダ名を識別するリスト '{0}' が存在しません。リストを確認してから再度プログラムを起動してください。", sFolderIndex_Path));
                fnWrite_LogFile("INFO", string.Format("警告：フォルダ名を識別するリスト '{0}' が存在しません。リストを確認してから再度プログラムを起動してください。", sFolderIndex_Path));
                //Console.WriteLine("終了する場合は何かキーを押してください...");

                Console.WriteLine("Enterキーを押すと画面を閉じます...");

                Console.ReadKey();
                return;
            }


            Program prog = new Program();
            prog.start();

        }

        /* 起動している端末のIPアドレス取得 */
        private void fnGetIPAddress()
        {
            /// 指定した端末のIPアドレスを取得する
            IPHostEntry ipentry = Dns.GetHostEntry(Dns.GetHostName());

            foreach (IPAddress ipaddr in ipentry.AddressList)
            {
                if (ipaddr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    ip = ipaddr.ToString();
                    break;
                }
            }
        }

        /* */
        private void start()
        {
            fnGet_IniFile_Info();
            Console.WriteLine("ファイルを{0}へ移動します。", move_folder_path);

            /* 起動している端末のIPアドレス取得 */
            fnGetIPAddress();

            /* クラサバ関係 */
            IPAddress local_ip = IPAddress.Parse(ip);
            server = new TcpListener(local_ip, port);
            server.Start();    // 接続の待機を開始

            Console.WriteLine("スキャンフォルダ監視プログラムと接続する場合は「IP：{0}、Port：{1}」を設定して下さい。", ip, port);
            Console.WriteLine("スキャンフォルダ監視プログラムから接続待機中...");

            thServer = new Thread(new ThreadStart(DoWork));　// 監視スレッド生成 
            thServer.Start();　// 監視スレッド開始 

            //Console.WriteLine("終了する場合は何かキーを押してください...");
            Console.ReadKey();

            fnthread_stop(); // サーバーの待機状態を中断し、スレッド停止

            fnWrite_LogFile("INFO", "ファイル転送プログラムを終了します。"); // ログファイル作成

        }


        /* inifileから移動先フォルダのpathを取得する */
        private static void fnGet_IniFile_Info()
        {
            // inifileパス設定
            string inifile_path = string.Format("{0}\\{1}", System.IO.Directory.GetCurrentDirectory(), inifile_name);

            bool writeflg = false;

            // inifileの存在確認
            if (System.IO.File.Exists(inifile_path))
            {
                // 存在する場合
                // iniファイルから文字列を取得
//                StringBuilder sb = new StringBuilder(int.MaxValue);
                StringBuilder sb = new StringBuilder(1024);
                GetPrivateProfileString("MoveFolder", "Path", "NoData", sb, Convert.ToUInt32(sb.Capacity), inifile_path);
                if (sb.ToString() == "NoData")
                    move_folder_path = "";
                else
                    move_folder_path = sb.ToString();
            }
            else
            {
                // 存在しない場合
                System.IO.StreamWriter sw = System.IO.File.CreateText(inifile_path);
                sw.Close();
                Console.WriteLine("移動先フォルダのパスを入力してください。");
                move_folder_path = Console.ReadLine();
                writeflg = true; // inifileへの書き込みフラグ有効
            }

            // Scanフォルダ存在確認
            while (true)
            {
                if (System.IO.Directory.Exists(move_folder_path))
                    break; // 存在する場合は抜ける
                else
                {
                    Console.WriteLine("移動先フォルダが存在しません。パスを入力してください。");
                    move_folder_path = Console.ReadLine();
                    writeflg = true; // inifileへの書き込みフラグ有効
                }
            }

            if (writeflg)
            {
                // inifileへ書き込み
                WritePrivateProfileString(
                    "MoveFolder",       // セクション名
                    "Path",             // キー名    
                    move_folder_path,
                    inifile_path
                );
            }
        }

        /*/////////////////////
         // ログファイル関係 //
         /////////////////////*/
        // ログフォルダの作成、存在確認
        private static void fnCreate_LogFolder()
        {
            // ログフォルダの存在確認
            sSave_log_Folder_Path = string.Format("{0}\\{1}", System.IO.Directory.GetCurrentDirectory(), "log");
            // ログフォルダが存在するか確認
            if (!System.IO.Directory.Exists(sSave_log_Folder_Path))
            {
                System.IO.Directory.CreateDirectory(sSave_log_Folder_Path);    // 存在しない場合はログフォルダ作成
            }
        }

        // ログファイルの作成、存在確認
        private static void fnWrite_LogFile(string sFlg, string sMsg)
        {
            string sCheckPath = string.Format("{0}\\{1}_{2}", sSave_log_Folder_Path, DateTime.Now.ToString("yyyy-MM-dd"), "FlCopy.log");

            if (sCheckPath == sSave_log_File_Path)
            {
                // ファイルが存在する場合
                swWriter = new StreamWriter(sCheckPath, true, Encoding.GetEncoding("Shift_JIS"));
                string sStr = string.Format("{0}\t{1}\t{2}", DateTime.Now.ToString("yyyy/MM/dd/ HH:mm:ss_fff"), sFlg, sMsg);
                swWriter.WriteLine(sStr);  // ファイルに書き込み
                swWriter.Flush();
                swWriter.Close();
            }
            else
            {
                // ログファイルが存在するか確認
                if (!System.IO.Directory.Exists(sCheckPath))
                {
                    System.IO.StreamWriter sw = System.IO.File.CreateText(sCheckPath);
                    sw.Close();     // ファイルを閉じる
                }

                swWriter = new StreamWriter(sCheckPath, true, Encoding.GetEncoding("Shift_JIS"));
                string sStr = string.Format("{0}\t{1}\t{2}", DateTime.Now.ToString("yyyy/MM/dd/ HH:mm:ss_fff"), sFlg, sMsg);
                swWriter.WriteLine(sStr);  // ファイルに書き込み
                swWriter.Flush();
                swWriter.Close();
                sSave_log_File_Path = sCheckPath;
            }

        }

        /* 監視プログラムからの接続監視スレッド */
        private void DoWork()
        {
            try
            {
                while (true)
                {
                    // 接続要求待ち
                    client = server.AcceptTcpClient();

                    if (client.Connected)
                    {
                        Console.WriteLine("スキャンフォルダ監視プログラムと接続しました。");
                        ThreadPool.QueueUserWorkItem(fndata_receive);
                    }
                }
            }
            catch (System.Threading.ThreadAbortException)
            {
                //Abortが呼び出されたとき
            }

        }


        /* サーバーの待機状態を中断し、スレッド停止 */
        private void fnthread_stop()
        {
            try
            {
                server.Stop();
                thServer.Abort();
            }
            catch
            {
                Console.WriteLine("監視スレッド：異常終了しました。");
            }
        }

        /* フォルダ監視プログラムからのメッセージを受信し、解析 */
        private void fndata_receive(Object stateInfo)
        {
            try
            {
                stream = client.GetStream();

                // フォルダ監視プログラムから送られたデータを受信する
                Byte[] bytes = new Byte[512];
                String data = null;
                int i;
                while ((i = stream.Read(bytes, 0, 512)) != 0)
                {
                    data = System.Text.Encoding.UTF8.GetString(bytes, 0, i);
                    string[] array_data = data.Split('?');
                    // 0 現在時刻
                    // 1 ファイルの作成日時
                    // 2 Create
                    // 3 path
                    // 4 テキストファイルの中身
                    
                    Console.WriteLine("受信：{0}、{1}、{2}、{3}、{4}", array_data[0], array_data[1], array_data[2], array_data[3], array_data[4]);
                    fnWrite_LogFile("INFO", string.Format("スキャンフォルダ監視プログラムから{0}を受信しました。", data)); // ログファイル作成

                    bool fileflg = System.IO.File.Exists(array_data[3]);
                    if (!fileflg)
                    {
                        Console.WriteLine(string.Format("{0}が存在しません。ファイルの移動はできませんでした。", array_data[3]));
                        fnWrite_LogFile("INFO", string.Format("{0}が存在しません。ファイルの移動はできませんでした。", array_data[3])); // ログファイル作成
                    }

                    // 指定したフォルダへ移動
                    fnCreateMoveFolder(array_data[4], array_data[3]);
                }

                client.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine("例外発生：" + e.StackTrace);
            }
            finally
            {
                //mySocket.Close();
            }
        }

        // 
        private void fnCreateMoveFolder(string txtdata, string movefile)
        {
            // 「_」分割して配列に格納する
            if (txtdata.Contains("_"))
            {
                string[] stArrayData = txtdata.Split('_');
                string id = stArrayData[0];
                string folder_index = stArrayData[1].Replace(Environment.NewLine, "");

                //intに変換できるか確かめる
                int check_id;
                if (int.TryParse(id, out check_id))
                {
                }
                else
                {
                    Console.WriteLine("テキストファイルの中にある、患者IDは数字ではありません。ご確認ください。");
                    fnWrite_LogFile("INFO", "テキストファイルの中にある、患者IDは数字ではありません。ご確認ください。"); // ログファイル作成
                }

                int check_folder_index;
                if (int.TryParse(folder_index, out check_folder_index))
                {
                }
                else
                {
                    Console.WriteLine("テキストファイルの中にある、フォルダIDは数字ではありません。ご確認ください。");
                    fnWrite_LogFile("INFO", "テキストファイルの中にある、フォルダIDは数字ではありません。ご確認ください。"); // ログファイル作成
                }

                int nIndex = Int32.Parse(folder_index);

                // 院内文書フォルダの直下に患者IDフォルダを作成・存在確認
                string id_folder_path = string.Format("{0}\\{1}", move_folder_path, id);
                bool flg = System.IO.Directory.Exists(id_folder_path);
                if (!flg)
                {
                    System.IO.Directory.CreateDirectory(id_folder_path);
                    Console.WriteLine(string.Format("{0}フォルダを作成しました。", id_folder_path));
                    fnWrite_LogFile("INFO", string.Format("{0}フォルダを作成しました。", id_folder_path)); // ログファイル作成
                }

                if (nIndex == 000)
                {
                 // 保険証などのファイルは文書フォルダを作成しないでIDフォルダの直下に移動
                    System.IO.File.Move(movefile, string.Format("{0}//{1}", id_folder_path, System.IO.Path.GetFileName(movefile)));
                    Console.WriteLine(string.Format("{0}を{1}へ移動しました。", movefile, id_folder_path));
                    fnWrite_LogFile("INFO", string.Format("{0}を{1}へ移動しました。", movefile, id_folder_path)); // ログファイル作成
                }
                else
                {
                // 院内文書フォルダ - IDフォルダ 以下に各文書フォルダを作成
                    fnAllCreateFolder(id_folder_path, nIndex, movefile);
                }
            }
            else
            {
                Console.WriteLine("テキストファイルの中にある、データの分割ができませんでした。正しいデータかご確認ください。");
                fnWrite_LogFile("ERR", "テキストファイルの中にある、データの分割ができませんでした。正しいデータかご確認ください。"); // ログファイル作成

            }
        }

        // 院内文書フォルダ - IDフォルダ 以下に各フォルダを作成する
        private void fnAllCreateFolder(string id_folder_path, int folder_index, string movefile)
        {
            string sIndex = "";
            string sFolder_Name = "";
            int nIndex = 0;

            // StreamReader の新しいインスタンスを生成する
            System.IO.StreamReader cReader = (new System.IO.StreamReader(sFolderIndex_Path, System.Text.Encoding.GetEncoding("shift_jis")));

            // 読み込みできる文字がなくなるまで繰り返す
            while (cReader.Peek() >= 0)
            {
                sIndex = "";
                sFolder_Name = "";
                nIndex = 0;

                // ファイルを 1 行ずつ読み込む
                string stBuffer = cReader.ReadLine();
                string[] stArrayData = stBuffer.Split(':');
                sIndex = stArrayData[0];
                sFolder_Name = stArrayData[1];
                nIndex = Int32.Parse(sIndex);

                // 
                if (folder_index == nIndex)
                {
                    string filepath_kind = string.Format("{0}\\{1}", id_folder_path, sFolder_Name);
                    bool fileflg = System.IO.Directory.Exists(filepath_kind);
                    if (!fileflg)
                    {
                        System.IO.Directory.CreateDirectory(filepath_kind);
                        Console.WriteLine(string.Format("{0}フォルダを作成しました。", filepath_kind));
                        fnWrite_LogFile("INFO", string.Format("{0}フォルダを作成しました。", filepath_kind)); // ログファイル作成
                    }

                    System.IO.File.Move(movefile, string.Format("{0}//{1}", filepath_kind, System.IO.Path.GetFileName(movefile)));
                    Console.WriteLine(string.Format("{0}を{1}へ移動しました。", movefile, filepath_kind));
                    fnWrite_LogFile("INFO", string.Format("{0}を{1}へ移動しました。", movefile, filepath_kind)); // ログファイル作成
                }
            }
            // cReader を閉じる
            cReader.Close();
        }
    }
}
