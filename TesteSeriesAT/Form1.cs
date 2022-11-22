using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web.Services.Protocols;
using System.Windows.Forms;

namespace TesteSeriesAT
{
    public class WebServiceCRYPT
    {
        public WebServiceCRYPT(string senha)
        {
            GerarEncriptacao(senha, ref _SenhaEncriptada, ref _DataEncriptada, ref _ChaveSimetricaEncriptada);
        }

        private string _SenhaEncriptada;
        public string SenhaEncriptada
        {
            get
            {
                return _SenhaEncriptada;
            }
        }

        private string _DataEncriptada;
        public string DataEncriptada
        {
            get
            {
                return _DataEncriptada;
            }
        }

        private string _ChaveSimetricaEncriptada;
        public string ChaveSimetricaEncriptada
        {
            get
            {
                return _ChaveSimetricaEncriptada;
            }
        }

        private void GerarEncriptacao(string in_senha, ref string out_senha, ref string out_criacao, ref string out_chave_simetrica)
        {
            X509Certificate2 certificado = new X509Certificate2();
            certificado.Import("ChaveCifraPublicaAT2023.cer");



            string publicKey = certificado.PublicKey.Key.ToXmlString(false);
            string DataCriacao = DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ff") + "Z";

            RijndaelManaged rijndaelCipher = new RijndaelManaged();
            rijndaelCipher.GenerateKey();
            rijndaelCipher.Mode = CipherMode.ECB;
            rijndaelCipher.Padding = PaddingMode.PKCS7;

            string simetrickey = rijndaelCipher.Key.ToString();
            Byte[] chaveSimetrica = rijndaelCipher.Key;
            SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
            rijn.Key = rijndaelCipher.IV;
            rijn.IV = rijndaelCipher.IV;
            rijn.Mode = CipherMode.ECB;

            MemoryStream msPassFinancas = new MemoryStream();
            CryptoStream csPassFinancas = new CryptoStream(msPassFinancas, rijn.CreateEncryptor(rijn.Key, rijn.IV), CryptoStreamMode.Write);
            using (StreamWriter swPassFinancas = new StreamWriter(csPassFinancas))
            {
                swPassFinancas.Write(in_senha);
            }

            MemoryStream msDataCriacao = new MemoryStream();
            CryptoStream csDataCriacao = new CryptoStream(msDataCriacao, rijn.CreateEncryptor(rijn.Key, rijn.IV), CryptoStreamMode.Write);
            using (StreamWriter swDataCriacao = new StreamWriter(csDataCriacao))
            {
                swDataCriacao.Write(DataCriacao);
            }

            out_senha = Convert.ToBase64String(msPassFinancas.ToArray());
            out_criacao = Convert.ToBase64String(msDataCriacao.ToArray());

            RSACryptoServiceProvider AlgRSA = new RSACryptoServiceProvider();
            AlgRSA.FromXmlString(publicKey);

            Byte[] Chave = AlgRSA.Encrypt(rijn.Key, false);
            out_chave_simetrica = Convert.ToBase64String(Chave);
        }
    }


    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            SoapLoggerExtension ola = new SoapLoggerExtension();

            SeriesAT.SeriesWSService proxy = new SeriesAT.SeriesWSService();
            X509Certificate2Collection certCollection = new X509Certificate2Collection();
            certCollection.Import("TesteWebservices.pfx", "TESTEwebservice", X509KeyStorageFlags.DefaultKeySet);
            int x = 0;
            for (x = 0; x < certCollection.Count; x++)
            {
                proxy.ClientCertificates.Add(certCollection[x]);
            }

            WebServiceCRYPT crypt = new WebServiceCRYPT("testes1234");

            proxy.Url = "https://servicos.portaldasfinancas.gov.pt:722/SeriesWSService";
            proxy.SecurityToken = new GENERIC.Security("599999993/37", crypt.SenhaEncriptada, crypt.DataEncriptada, crypt.ChaveSimetricaEncriptada);
            try
            {
                SeriesAT.seriesResp resp = proxy.RegistaSerie(SeriesAT.TipoDocumentoSerie.FT , "22", 1, new DateTime(2022,1,1), "1000");
                if (resp.infoResultOper.codResultOper == "2001")
                {
                    // Série registada com sucesso
                    String codigo = resp.infoSerie.codValidacaoSerie;
                    Console.WriteLine(codigo);
                }
                else
                {
                    String erro = "(" + resp.infoResultOper.codResultOper + "): " + resp.infoResultOper.msgResultOper;
                    Console.WriteLine(erro);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message );
            }
        }
    }


    public class SoapLoggerExtension : SoapExtension
    {
        private Stream oldStream;
        private Stream newStream;

        public override object GetInitializer(LogicalMethodInfo methodInfo, SoapExtensionAttribute attribute)
        {
            return null;
        }

        public override object GetInitializer(Type serviceType)
        {
            return null;
        }

        public override void Initialize(object initializer)
        {
        }

        public override System.IO.Stream ChainStream(System.IO.Stream stream)
        {
            oldStream = stream;
            newStream = new MemoryStream();
            return newStream;
        }

        private void logSub(SoapMessage message, string str)
        {
        }
        public override void ProcessMessage(SoapMessage message)
        {
            switch (message.Stage)
            {
                case  SoapMessageStage.BeforeSerialize:
                    {
                        try
                        {
                            File.WriteAllText("soapAT.log", "");
                        }
                        catch (Exception ex)
                        {
                        }

                        break;
                    }

                case  SoapMessageStage.AfterSerialize:
                    {
                        Log(message, "AfterSerialize");
                        CopyStream(newStream, oldStream);
                        newStream.Position = 0;
                        break;
                    }

                case  SoapMessageStage.BeforeDeserialize:
                    {
                        CopyStream(oldStream, newStream);
                        Log(message, "BeforeDeserialize");
                        break;
                    }

                case  SoapMessageStage.AfterDeserialize:
                    {
                        break;
                    }
            }
        }

        public void Log(SoapMessage message, string stage)
        {
            newStream.Position = 0;
            string contents = "";
            StreamReader reader = new StreamReader(newStream);
            contents += reader.ReadToEnd();
            try
            {
                File.AppendAllText("soapAT.log", contents.Replace("<", "\n" + "<").Replace("\n" + "</", "</"));
            }
            catch (Exception ex)
            {
            }

            newStream.Position = 0;
        }

        private void ReturnStream()
        {
            CopyAndReverse(newStream, oldStream);
        }

        private void ReceiveStream()
        {
            CopyAndReverse(newStream, oldStream);
        }

        public void ReverseIncomingStream()
        {
            ReverseStream(newStream);
        }

        public void ReverseOutgoingStream()
        {
            ReverseStream(newStream);
        }

        public void ReverseStream(Stream stream)
        {
            TextReader tr = new StreamReader(stream);
            string str = tr.ReadToEnd();
            char[] data = str.ToCharArray();
            Array.Reverse(data);
            string strReversed = new string(data);
            TextWriter tw = new StreamWriter(stream);
            stream.Position = 0;
            tw.Write(strReversed);
            tw.Flush();
        }

        private void CopyAndReverse(Stream from, Stream to)
        {
            TextReader tr = new StreamReader(from);
            TextWriter tw = new StreamWriter(to);
            string str = tr.ReadToEnd();
            char[] data = str.ToCharArray();
            Array.Reverse(data);
            string strReversed = new string(data);
            tw.Write(strReversed);
            tw.Flush();
        }

        private void CopyStream(Stream fromStream, Stream toStream)
        {
            try
            {
                StreamReader sr = new StreamReader(fromStream);
                StreamWriter sw = new StreamWriter(toStream);
                sw.WriteLine(sr.ReadToEnd());
                sw.Flush();
            }
            catch (Exception ex)
            {
                string message = string.Format("CopyStream failed because: {0}", ex.Message);
                Console.WriteLine(ex.Message);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class SoapLoggerExtensionAttribute : SoapExtensionAttribute
    {
        private int _priority = 1;

        public override int Priority
        {
            get
            {
                return _priority;
            }
            set
            {
                _priority = value;
            }
        }

        public override System.Type ExtensionType
        {
            get
            {
                return typeof(SoapLoggerExtension);
            }
        }
    }

}
