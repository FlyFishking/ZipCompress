using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace Founder.Controls
{
    /// <summary>
    /// 文件及流方式打包服务
    /// 1、支持物理文件、流文件方式打包
    /// 2、支持整个文件夹打包
    /// <example>
    ///        using (CompressService zipsvc = new CompressService(zipOutFile, password, true))
    ///        {
    ///            zipsvc.ResetBufferSize(1024 * 1024 * 30);
    ///            zipsvc.AddFolder(@"D:\1\2", "*.xml");
    ///            //zipsvc.AddFile(@"D:\1.xml");
    ///            //zipsvc.AddFile(@"1\2\3.xml",stream);
    ///        }
    /// </example>
    /// </summary>
    public class CompressService : IDisposable
    {
        #region 公开属性
        /// <summary>
        /// 读写流时占用的内存大小
        /// </summary>
        public int? BufferSize { get; set; }

        /// <summary>
        /// 0-9 means best compression; Default 6;
        /// </summary>
        public int CompressLevel { get; private set; }

        public string Password { get; protected set; }

        //public bool AutoResoveFileConfilct { get; set; }

        /// <summary>
        /// ZIP包文件名称
        /// </summary>
        public string ZipOutPutFileName { get { return Path.GetFileName(zipOutPutFile); } }

        #endregion

        #region 压缩事件
        /// <summary>
        /// 添加压缩文件事件
        /// Params：1-ZIP包名称；2-文件名称；3-总大小；4-当前大小；
        /// </summary>
        public event Action<string, string, long, long> OnDoingCompress;

        /// <summary>
        /// 压缩完成事件
        /// Params：1-ZIP包名称；2-ZIP包大小；
        /// </summary>
        public event Action<string, long> OnCompressComplete;

        /// <summary>
        /// 取消压缩事件
        /// Params：ZIP包名称
        /// </summary>
//        public event Action<string> OnCancelCompress;
        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="zipFile">压缩包文件名称</param>
        /// <param name="pwd">密码</param>
        /// <param name="fileConfilctIncrease">是否处理ZIP命名冲突问题</param>
        public CompressService(string zipFile, string pwd, bool fileConfilctIncrease)
        {
            CompressLevel = 6;
            Password = pwd;
            if (fileConfilctIncrease)
            {
                zipFile = ResoveFileConflict(zipFile);
            }
            zipOutPutFile = zipFile;
            StartCreateZipStream(zipOutPutFile);
        }

        /// <summary>
        /// 内部调用
        /// </summary>
        private CompressService()
        {

        }
        #endregion

        #region 公开实例方法

        /// <summary>
        /// 设置缓冲区内存空间大小
        /// </summary>
        /// <param name="length"></param>
        public void ResetBufferSize(int length)
        {
            BufferSize = length;
        }

        //public void ChangePassword(string pwd)
        //{
        //    Password = pwd;
        //}

        #endregion

        #region 文件压缩
        /// <summary>
        /// 压缩文件
        /// </summary>
        /// <param name="file"></param>
        public void AddFile(string file)
        {
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
            {
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    AddFile(Path.GetFileName(file), fs);
                }
                //using (var entry = new MyZipEntry(Path.GetFileName(file)))
                //{
                //    zipOutStream.PutNextEntry(entry);
                //    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                //    {
                //        int readLength;
                //        do
                //        {
                //            readLength = fs.Read(buffer, 0, buffer.Length);
                //            zipOutStream.Write(buffer, 0, readLength);
                //        } while (readLength == buffer.Length);
                //    }
                //}
            }
        }

        /// <summary>
        /// 压缩目录
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="searchParten">符合通配符的文件才会压缩</param>
        /// <param name="option">与搜索通配符匹配使用，指示是否在子目录中搜索</param>
        public void AddFolder(string folderPath, string searchParten = "*.*", SearchOption option = SearchOption.AllDirectories)
        {
            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                var files = Directory.GetFiles(folderPath, searchParten, option);
                foreach (var file in files)
                {
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                    {
                        //string path = file.Substring((folderPath.EndsWith("\\") || folderPath.EndsWith("/")) ? folderPath.Length : folderPath.Length + 1);
                        string path = file.Substring(folderPath.Length);
                        path = path.StartsWith("\\") ? path.Substring(1) : path;
                        AddFile(path, fs);
                    }
                }
            }
        }

        /// <summary>
        /// 流方式压缩文件
        /// </summary>
        /// <param name="name">压缩包中显示的文件名称<remarks>如果需要组织目录结构，则此处需要手动构建斜杠分割的目录层级字符串</remarks></param>
        /// <param name="content">文件内容</param>
        public void AddFile(string name, Stream content)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (buffer == null || buffer.Length == 0)
            {
                buffer = new byte[BufferSize ?? DefaultBufferSize];
            }

            var totalFileLength = content.Length;
            long writeLength = 0;
            using (var entry = new MyZipEntry(name))
            {
                zipOutStream.PutNextEntry(entry);
                int readLength = 0;
                do
                {
                    readLength = content.Read(buffer, 0, buffer.Length);
                    zipOutStream.Write(buffer, 0, readLength);
                    if (OnDoingCompress != null)
                    {
                        OnDoingCompress(ZipOutPutFileName, name, totalFileLength, writeLength += readLength);
                    }
                } while (readLength == buffer.Length);
            }
        }
        #endregion

        #region 压缩结束处理及资源释放 开始
        public void Dispose()
        {
            if (this != null)
            {
            }
            buffer = null;
            long zipLength = zipOutStream.Length;
            zipOutStream.Finish();
            zipOutStream.Dispose();
            if (OnCompressComplete != null)
            {
                OnCompressComplete(ZipOutPutFileName, zipLength);
            }
            //GC.Collect();
        }
        #endregion 压缩结束处理及资源释放 结束

        #region 私有属性
        //private FileStream zipFileStream;

        /// <summary>
        /// ZIP包存储路径
        /// </summary>
        private readonly string zipOutPutFile;

        /// <summary>
        /// ZIP包输出流
        /// </summary>
        private ZipOutputStream zipOutStream;

        /// <summary>
        /// 读写流时占用的内存大小
        /// </summary>
        private const int DefaultBufferSize = 1024*1024*100; //Default 100MB

        /// <summary>
        /// 读写流时占用的内存数据缓存区
        /// </summary>
        private byte[] buffer;

        /// <summary>
        /// 暂停
        /// </summary>
        private bool isPause;
        #endregion

        #region 私有方法 开始

        /// <summary>
        /// 初始化压缩包
        /// </summary>
        /// <param name="file">ZIP包存放路径</param>
        private void StartCreateZipStream(string file)
        {
            Path.GetDirectoryName(file).TryCreateFolder();
            //zipFileStream = new FileStream(zipFile, FileMode.Create, FileAccess.Write);
            zipOutStream = new ZipOutputStream(new FileStream(file, FileMode.Create, FileAccess.Write));
            zipOutStream.SetLevel(CompressLevel);
            if (!string.IsNullOrEmpty(Password))
            {
                zipOutStream.Password = Password;
            }
        }

        #endregion 私有方法 结束

        #region 静态 补充方法：文件加密、文件明明冲突处理等 开始

        /// <summary>
        /// 解决文件命名冲突问题：解决办法在重复文件名后增加序号（NO.）
        /// </summary>
        /// <param name="file">文件路径</param>
        /// <returns></returns>
        public static string ResoveFileConflict(string file)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                return file;
            }
            string folder = Path.GetDirectoryName(file);
            string fileName = Path.GetFileNameWithoutExtension(file);
            string fileExt = Path.GetExtension(file);
            var parten = string.Format("{0}*{1}", fileName, fileExt);
            var files = Directory.GetFiles(folder, parten, SearchOption.TopDirectoryOnly);
            int fileCnt = files.Length;
            //文件冲突，改名
            if (fileCnt > 0)
            {
                file = file.Insert(file.LastIndexOf("."), string.Format("（{0}）", fileCnt));
            }
            return file;
        }

        /// <summary>
        /// 对文件进行MD5加密，密钥放到文件名中
        /// </summary>
        /// <param name="file">需要加密的文件路径</param>
        /// <param name="md5File">加密后的文件名称，加密过程中出错则返回NULL</param>
        /// <returns></returns>
        public static bool TryEncryptMD5(string file, out string md5File)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                md5File = null;
                return false;
            }
            string md5 = Founder.Helper.Helper.MD5Helper.getMD5Hash(file);
            if (md5 == "wrong")//加密失败
            {
                md5File = null;
                return false;
            }

            //改名
            md5File = file.Insert(file.LastIndexOf("."), "_" + md5);
            try
            {
                if (File.Exists(md5File))
                {
                    File.Delete(md5File);
                }
                File.Move(file, md5File);//移动
                return true;
            }
            catch (Exception e)
            {
                md5File = null;
                return false;
            }
        }

        /// <summary>
        /// 打包文件到一个压缩包
        /// </summary>
        /// <example>
        ///    Action string, long, long onCompressing = (fileName, totalSize, readingSize) => {
        ///        var content = string.Format("正在添加文件{0}，{1}/{2}", fileName, totalSize, readingSize);
        ///    };
        ///    Action long  onComplete = (zipSize) => {
        ///        var content = "压缩完成，压缩后文件总大小为" + zipSize;
        ///    };
        ///    CompressService.Compress(@"C:\Users\Administrator\Desktop\分析报告.docx", @"d:\3\4\file.zip", "a", 6, 20, onCompressing, onComplete);
        /// </example>
        /// <param name="files">文件集</param>
        /// <param name="zipFile"></param>
        /// <param name="pwd"></param>
        /// <param name="level">压缩级别0-9</param>
        /// <param name="bufferSize">缓冲区大小（MB）</param>
        /// <param name="onDoingCompress">当前添加到压缩包的文件名称、总大小、写入大小</param>
        /// <param name="onCompressComplete">压缩结束时压缩包的大小</param>
        public static void Compress(string[] files, string zipFile, string pwd, int level, int bufferSize, Action<string, long, long> onDoingCompress, Action<long> onCompressComplete)
        {
            if (files == null || files.Length == 0)
            {
                return;
            }
            //创建压缩包存储目录
            Path.GetDirectoryName(zipFile).TryCreateFolder();
            using (var outStream = new ZipOutputStream(new FileStream(zipFile, FileMode.Create, FileAccess.Write)))
            {
                var buffer = new byte[1024 * 1024 * bufferSize];
                outStream.Password = string.IsNullOrEmpty(pwd) ? null : pwd;
                outStream.SetLevel(level);

                foreach (var file in files)
                {
                    if (string.IsNullOrEmpty(file) || !File.Exists(file))
                    {
                        break;
                    }
                    //添加文件开始压缩
                    using (var entry = new MyZipEntry(Path.GetFileName(file)))
                    {
                        entry.DateTime = DateTime.Now;
                        outStream.PutNextEntry(entry);

                        using (var content = new FileStream(file, FileMode.Open, FileAccess.Read))
                        {
                            var fileLength = content.Length;
                            int readLength = 0;
                            long writeLength = 0;
                            do
                            {
                                readLength = content.Read(buffer, 0, buffer.Length);
                                outStream.Write(buffer, 0, readLength);
                                if (onDoingCompress != null)
                                {
                                    //Params：1-文件名、2-总长度、3-当前长度
                                    onDoingCompress(entry.Name, fileLength, writeLength += readLength);
                                }
                            } while (readLength == buffer.Length);
                        }
                    }
                }

                var zipLength = outStream.Length;
                outStream.Finish();
                outStream.Dispose();

                if (onCompressComplete != null)
                {
                    onCompressComplete(zipLength);
                }
            }
        }

        /// <summary>
        /// 压缩整个文件夹
        /// </summary>
        /// <example>
        ///    Action string, long, long onCompressing = (fileName, totalSize, readingSize) => {
        ///        var content = string.Format("正在添加文件{0}，{1}/{2}", fileName, totalSize, readingSize);
        ///    };
        ///    Action long onComplete = (zipSize) => {
        ///        var content = "压缩完成，压缩后文件总大小为" + zipSize;
        ///    };
        ///    CompressService.Compress(@"C:\Users\Administrator\Desktop\export\新建文件夹", null, @"d:\3\4\folder.zip", "a", 6, 20, onCompressing, onComplete);
        /// </example>
        /// <param name="folder">待压缩的文件夹路径</param>
        /// <param name="zipFile">ZIP文件存放地址</param>
        /// <param name="pwd"></param>
        /// <param name="level">压缩级别0-9</param>
        /// <param name="bufferSize">缓冲区大小（MB）</param>
        /// <param name="onDoingCompress">压缩过程事件:当前添加到压缩包的文件名称、总大小、已经写入的大小</param>
        /// <param name="onCompressComplete">压缩结束事件:压缩结束时压缩包的大小</param>
        /// <param name="searchParten">压缩文件夹的过滤器</param>
        /// <param name="option">与搜索通配符匹配使用，指示是否在子目录中搜索</param>
        public static void Compress(string folder, string zipFile, string pwd, int level, int bufferSize, Action<string, long, long> onDoingCompress, Action<long> onCompressComplete, string searchParten, SearchOption option)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return;
            }

            searchParten = searchParten.GetWithDefault("*.*");
            var files = Directory.GetFiles(folder, searchParten, option);
            if (files.Length == 0)
            {
                return;
            }

            //创建压缩包存储目录
            Path.GetDirectoryName(zipFile).TryCreateFolder();

            using (var outStream = new ZipOutputStream(new FileStream(zipFile, FileMode.Create, FileAccess.Write)))
            {
                var buffer = new byte[1024 * 1024 * bufferSize];
                outStream.Password = string.IsNullOrEmpty(pwd) ? null : pwd;
                outStream.SetLevel(level);

                foreach (var file in files)
                {
                    string name = file.Substring(folder.Length);
                    name = name.StartsWith("\\") ? name.Substring(1) : name;
                    //添加文件开始压缩
                    using (var entry = new MyZipEntry(name))
                    {
                        entry.DateTime = DateTime.Now;
                        outStream.PutNextEntry(entry);

                        using (var content = new FileStream(file, FileMode.Open, FileAccess.Read))
                        {
                            var fileLength = content.Length;
                            int readLength;
                            long writeLength = 0;
                            do
                            {
                                readLength = content.Read(buffer, 0, buffer.Length);
                                outStream.Write(buffer, 0, readLength);
                                if (onDoingCompress != null)
                                {
                                    //Params：1-文件名、2-总长度、3-当前长度
                                    onDoingCompress(entry.Name, fileLength, writeLength += readLength);
                                }
                            } while (readLength == buffer.Length);
                        }
                    }
                }

                var zipLength = outStream.Length;
                outStream.Finish();
                outStream.Dispose();

                if (onCompressComplete != null)
                {
                    onCompressComplete(zipLength);
                }
            }
        }

        /// <summary>
        /// 解压数据包
        /// </summary>
        /// <param name="file">ZIP包路径</param>
        /// <param name="targetFolder">解压到文件夹</param>
        /// <param name="pwd">密码</param>
        /// <param name="onDoingUncompress">解压过程通知事件（Params：1-文件名、2-总长度、3-当前长度）</param>
        /// <returns>解压结果</returns>
        public static UnCompressStatus Uncompress(string file, string targetFolder, string pwd, Action<string, long, long> onDoingUncompress)
        {
            if (!File.Exists(file))
            {
                return UnCompressStatus.ZipFileNotFound;
            }

            using (ZipInputStream inStream = new ZipInputStream(new FileStream(file, FileMode.Open, FileAccess.Read)))
            {
                inStream.Password = string.IsNullOrEmpty(pwd) ? null : pwd;
                ZipEntry zipEntry;
                while ((zipEntry = inStream.GetNextEntry()) != null)
                {
                    Path.Combine(targetFolder, Path.GetDirectoryName(zipEntry.Name)).TryCreateFolder();

                    using (var content = new FileStream(Path.Combine(targetFolder, zipEntry.Name), FileMode.Create, FileAccess.Write))
                    {
                        int readLength = 0;
                        long writeLength = 0;
                        var buffer = new byte[1024 * 1024 * 10];
                        do
                        {
                            try
                            {
                                readLength = inStream.Read(buffer, 0, buffer.Length);
                            }
                            catch (ZipException ex)
                            {
                                return UnCompressStatus.InvalidPassword;
                            }
                            content.Write(buffer, 0, readLength);
                            if (onDoingUncompress != null)
                            {
                                //Params：1-文件名、2-总长度、3-当前长度
                                onDoingUncompress(zipEntry.Name, inStream.Length, writeLength += readLength);
                            }
                        } while (readLength == buffer.Length);
                    }
                }
            }
            //return Directory.GetFiles(targetFolder, "*.*", SearchOption.AllDirectories);
            return UnCompressStatus.Success;
        }

        /// <summary>
        /// 文件大小单位转换：BYTE->MB
        /// </summary>
        /// <param name="byteSize"></param>
        /// <returns></returns>
        public static long ConvertSizeByteToMB(long byteSize)
        {
            return byteSize / (1024 * 1024);
        }

        public static ImportResult Uncompress(string file, string pwd, Action<string, string> onBuildSqlQueryComplete = null, Action<string, string, int> onZipFileReadComplete = null, Action<string> onZipReadComplete = null, Action<string, long> onBeginReading = null)
        {
            var result = ImportResult.NotRun;
            //var buffer = new byte[1024 * 1024 * bufferSize];
            using (ZipInputStream inStream = new ZipInputStream(new FileStream(file, FileMode.Open, FileAccess.Read)))
            {
                inStream.Password = string.IsNullOrEmpty(pwd) ? null : pwd;

                var queryHeader = new StringBuilder(500 * 10);
                var queryBody = new StringBuilder(500 * 10);

                ZipEntry zipEntry;

                #region 读取ZIP包中的文件，针对文件生成数据库插入语句 开始
                while ((zipEntry = inStream.GetNextEntry()) != null && zipEntry.Name.EndsWith(".xml"))
                {
                    if (onBeginReading != null)
                    {
                        onBeginReading(zipEntry.Name,zipEntry.Size);
                    }
                    //inStream.Read(buffer, 0, buffer.Length);
                    //string text = Encoding.UTF8.GetString(buffer);
                    XmlReader xmlContent;
                    try
                    {
                        xmlContent = System.Xml.XmlReader.Create(inStream);
                    }
                    catch (Exception e)
                    {
                        result = ImportResult.PWDError;
                        break;
                    }

                    string dbTableName = null;//datatabse table name
                    int dbRecordCnt = 0; //已经读取的记录数（SQL QUERY 语句数量）
                    StringBuilder sqlBuilder = new StringBuilder();
                    bool isFirstFiled = true;//构建SQL QUERY 字段时，用来判断是否添加逗号‘，’
                    while (xmlContent.Read())
                    {
                        switch (xmlContent.NodeType)
                        {
                            case System.Xml.XmlNodeType.Element:
                                //DATASET 序列化之后的xml root名称
                                if (xmlContent.Name.Equals("NewDataSet", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    break;
                                }
                                //开始读表信息：此处确认database TABLENAME
                                else if (dbTableName == null || xmlContent.Name == dbTableName) 
                                {
                                    if (dbTableName == null)
                                    {
                                        dbTableName = xmlContent.Name;
                                    }
                                    isFirstFiled = true;//准备读第一个字段
                                    queryHeader.AppendFormat("INSERT INTO {0} (", dbTableName);
                                    queryBody.Append("VALUES ( ");
                                }
                                /*开始读字段信息：
                                 * 读取时的NODETYPE 顺序：
                                 * 1、<j1_12701>1768</j1_12701>：Element->Text->EndElement->Whitespace
                                 * 2、<tbr />：Element->Whitespace
                                */
                                else
                                {
                                    //跳过主键字段
                                    if (Constant.DBTablePKs.Any(t => string.Compare(t, xmlContent.Name, true) == 0))
                                    {
                                        new CompressService().SkipField(xmlContent);
                                    }

                                    queryHeader.AppendFormat("{0}{1}", isFirstFiled ? "" : ", ", xmlContent.Name);
                                    xmlContent.Read();
                                    //hasValue = xmlContent.NodeType == System.Xml.XmlNodeType.Text;
                                    queryBody.AppendFormat("{0}{1}", isFirstFiled ? "" : ", ", Regex.IsMatch(xmlContent.Value, "\\n") ? Regex.Replace(xmlContent.Value, "\\s", "").GetWithDefault("NULL") : string.Format("'{0}'", xmlContent.Value));
                                    isFirstFiled = false;
                                }
                                //currFiledName = xmlContent.Name;
                                break;
                            //一条记录结束
                            case System.Xml.XmlNodeType.EndElement:
                                //文件读取结束
                                if (xmlContent.Name.Equals("NewDataSet", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    if (onZipFileReadComplete != null)
                                    {
                                        onZipFileReadComplete(zipEntry.Name, dbTableName, dbRecordCnt);
                                    }
                                }
                                    //读取记录结束，生成SQL插入语句结束
                                else if (xmlContent.Name == dbTableName)
                                {
                                    queryHeader.Append(") ");
                                    queryBody.Append("); ");
                                    sqlBuilder = queryHeader.Append(queryBody);
                                    //queryList.Add(sqlBuilder.ToString());
                                    dbRecordCnt++;
                                    if (onBuildSqlQueryComplete != null)
                                    {
                                        onBuildSqlQueryComplete(dbTableName, sqlBuilder.ToString());
                                    }
                                    queryBody.Clear();
                                    queryHeader.Clear();
//                                    if (dbRecordCnt == 200)
//                                    {
//                                        /*
//                                         */
//                                    }
                                }
                                break;
                        }
                    }
                }
                #endregion

                if (onZipReadComplete!=null)
                {
                    onZipReadComplete(file);
                }
                result = ImportResult.Success;
            }
            return result;
        }

        private void SkipField(XmlReader xmlContent)
        {
            do
            {
                xmlContent.Read();
            } while (xmlContent.NodeType != XmlNodeType.Element);
        }

        #endregion 补充方法：文件加密、文件明明冲突处理等 结束
    }

    public static class FileExtension
    {
        /// <summary>
        /// 创建文件夹
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        public static bool TryCreateFolder(this string dir)
        {
            if (string.IsNullOrEmpty(dir))
            {
                return false;
            }
            //dir = Path.GetDirectoryName(dir);
            if (!Directory.Exists(dir))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return true;
        }
    }

    public static class StringExtension
    {
        public static string GetWithDefault(this string str, string defaultValue)
        {
            return string.IsNullOrEmpty(str) ? defaultValue : str;
        }
        
    }

    public static class BufferExtension
    {
        public static byte[] ToBytes(this Stream stream)
        {
            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);
            stream.Seek(0, SeekOrigin.Begin);
            return bytes;
        }

        public static byte[] ToBytes(this string str, Encoding codeType = null)
        {
            if (Equals(codeType, Encoding.ASCII))
            {
                return Encoding.ASCII.GetBytes(str);
            }
            else if (Equals(codeType, Encoding.UTF8))
            {
                return Encoding.UTF8.GetBytes(str);
            }
            else if (Equals(codeType, Encoding.Unicode))
            {
                return Encoding.Unicode.GetBytes(str);
            }
            return Encoding.Default.GetBytes(str);
        }

        public static string ToString(this byte[] buffer, Encoding codeType = null)
        {
            if (Equals(codeType, Encoding.ASCII))
            {
                return Encoding.ASCII.GetString(buffer);
            }
            else if (Equals(codeType, Encoding.UTF8))
            {
                return Encoding.UTF8.GetString(buffer);
            }
            else if (Equals(codeType, Encoding.Unicode))
            {
                return Encoding.Unicode.GetString(buffer);
            }
            return Encoding.Default.GetString(buffer);
        }

        public static string ToString(this MemoryStream stream, Encoding codeType = null)
        {
            return stream.GetBuffer().ToString(codeType);
        }

        public static string ToString(this Stream stream, Encoding codeType = null)
        {
            return stream.ToBytes().ToString(codeType);
        }

        public static MemoryStream ToStream(this Stream stream, long offset, int bufferSize, Func<long, long, bool> onPerBufferReading = null)
        {
            if (offset > stream.Length)
            {
                return null;
            }
            if (stream.CanSeek)
            {
                stream.Seek(offset, SeekOrigin.Begin);
            }
            var buffer = new byte[1024*1024*bufferSize];
            using (var ms = new MemoryStream())
            {
                int readLength;
                long writeLength = 0;
                do
                {
                    readLength = stream.Read(buffer, 0, buffer.Length);
                    ms.Write(buffer, 0, readLength);
                    if (onPerBufferReading != null)
                    {
                        //Params：1-总长度、2-当前读取长度
                        if (onPerBufferReading(stream.Length, writeLength += readLength))
                        {
                            break;
                        }
                    }
                } while (readLength == buffer.Length);
                return ms;
            }
        }

        public static MemoryStream ToStream(this byte[] buffer)
        {
            using (var ms = new MemoryStream())
            {
                ms.Write(buffer, 0, buffer.Length);
                ms.Seek(0, SeekOrigin.Begin);
                return ms;
            }
        }

        public static MemoryStream ToStream(this string content, Encoding codeType = null)
        {
            using (var ms = new MemoryStream(content.ToBytes(codeType)))
            {
                ms.Seek(0, SeekOrigin.Begin);
                return ms;
            }
        }
    }

    public class MyZipEntry : ZipEntry, IDisposable
    {
//        private readonly ZipEntry entry;
        public MyZipEntry(string name, bool isUnicode = true)
            : base(name)
        {
            base.IsUnicodeText = isUnicode;
        }

        public void Dispose()
        {
            if (this != null)
            {

            }
            //GC.SuppressFinalize(this);
        }
    }

    //public class ZipHelper
    //{
    //    private static readonly int bufferSize = 1024 * 1024 * 30; //30MB
    //    /// <summary>
    //    /// 压缩文件
    //    /// </summary>
    //    /// <param name="files">待压缩的文件集合</param>
    //    /// <param name="zipOutFile">压缩包存放路径</param>
    //    /// <param name="password">密码设置</param>
    //    /// <returns></returns>
    //    public static bool CompressFiles(string[] files, string zipOutFile, string password)
    //    {
    //        using (CompressService zipsvc = new CompressService(zipOutFile, password, true))
    //        {
    //            zipsvc.ResetBufferSize(bufferSize);
    //            foreach (var file in files)
    //            {
    //                zipsvc.AddFile(file);
    //            }
    //        }
    //        return File.Exists(zipOutFile);
    //    }

    //    /// <summary>
    //    /// 把每个文件都单独压缩成独立的ZIP包，并输出到指定位置
    //    /// </summary>
    //    /// <param name="files">待压缩的文件集合</param>
    //    /// <param name="zipOutFolder">存放压缩包的目录</param>
    //    /// <param name="password"></param>
    //    /// <returns>是否全部压缩成功</returns>
    //    public static bool CompressPreFile(string[] files, string zipOutFolder, string password)
    //    {
    //        int errorCnt = 0;
    //        foreach (var file in files)
    //        {
    //            var zipOutFile = System.IO.Path.Combine(zipOutFolder, Path.GetFileNameWithoutExtension(file) + ".zip");
    //            using (CompressService zipsvc = new CompressService(zipOutFile, password, true))
    //            {
    //                zipsvc.ResetBufferSize(bufferSize);
    //                zipsvc.AddFile(file);
    //            }
    //            if (!File.Exists(zipOutFile))
    //            {
    //                errorCnt++;
    //            }
    //        }
    //        return errorCnt == 0;
    //    }

    //    /// <summary>
    //    /// 压缩整个文件夹
    //    /// </summary>
    //    /// <param name="path">待压缩的文件夹路径</param>
    //    /// <param name="zipOutFile">压缩包存放路径</param>
    //    /// <param name="password">密码设置</param>
    //    /// <param name="parten">过滤器</param>
    //    public static bool CompressFolder(string path, string zipOutFile, string password, string parten = "*.*")
    //    {
    //        using (CompressService zipsvc = new CompressService(zipOutFile, password, true))
    //        {
    //            zipsvc.ResetBufferSize(bufferSize);
    //            zipsvc.AddFolder(path, parten);
    //        }
    //        return File.Exists(zipOutFile);
    //    }
    //}
}
