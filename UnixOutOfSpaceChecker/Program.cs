using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnixOutOfSpaceChecker
{
    class Program
    {
        /*
         * Note: This program uses the mono runtime on Unix Machines.  You'll need to at least install mono-runtime, but may have to install mono-devel
         * depending on what options you choose in this program's config file (like the Cert hack).
         * 
         * So for example on Ubuntu 14.04 LTS or later you'd do...(of of them, or just the ones you need - depending on your needs of course)
         * 
         * apt-get install mono-runtime
         * apt-get install mono-devel
         * mozroots --import --ask-remove --machine
         * certmgr -ssl smtps://smtp.gmail.com:465
         * 
         */

        static private string DFLocation = new System.Configuration.AppSettingsReader().GetValue("DFLocation", System.Type.GetType("System.String")).ToString();
        static private int Limit = Convert.ToInt32(new System.Configuration.AppSettingsReader().GetValue("PercentLimit", System.Type.GetType("System.Int32")));

        static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                if (args[0].ToString().ToUpper() == "PASSWORD")
                {
                    Console.Write("Enter Password for your Email Address: ");
                    Console.WriteLine("Put \"" + EncryptPassword(Console.ReadLine()) + "\" in the config file.");
                }
            }
            else
            {
                if (System.IO.File.Exists(DFLocation))
                {
                    bool NeedToNotify = false;

                    System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo();
                    info.FileName = DFLocation;
                    info.Arguments = "-h --output=pcent";
                    info.UseShellExecute = false;
                    info.RedirectStandardOutput = true;


                    System.Diagnostics.Process p = new System.Diagnostics.Process();
                    p.StartInfo = info;
                    p.Start();
                    System.IO.StreamReader output = p.StandardOutput;
                    string usage = output.ReadToEnd();
                    string[] lines = usage.Split('\n');
                    foreach (string line in lines)
                    {
                        try
                        {
                            if (Convert.ToInt32(line.Trim().Replace("%", "")) > Limit)
                            {
                                NeedToNotify = true;
                                break;
                            }
                        }
                        catch
                        {
                            //unable to convert result from df to an integer - skip this volume.
                        }
                    }

                    if (NeedToNotify)
                    {
                        SendEmailNotification();
                    }
                }
                else
                {
                    Console.WriteLine("df utility not found.");
                }
            }
        }

        static private void SendEmailNotification()
        {
            System.Net.Mail.SmtpClient cli = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587);
            cli.EnableSsl = true;
            cli.DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network;
            cli.UseDefaultCredentials = false;
            cli.Credentials = new System.Net.NetworkCredential(new System.Configuration.AppSettingsReader().GetValue("GMailUserName", System.Type.GetType("System.String")).ToString(), DecryptPassword(new System.Configuration.AppSettingsReader().GetValue("GMailPassword", System.Type.GetType("System.String")).ToString()));

            System.Net.Mail.MailMessage msg = new System.Net.Mail.MailMessage(new System.Configuration.AppSettingsReader().GetValue("GMailUserName", System.Type.GetType("System.String")).ToString(), new System.Configuration.AppSettingsReader().GetValue("Notify", System.Type.GetType("System.String")).ToString());
            msg.Subject = "SERVER RUNNING OUT OF SPACE (" + new System.Configuration.AppSettingsReader().GetValue("MachineName", System.Type.GetType("System.String")).ToString() + ")";
            msg.Body = "The server (" + new System.Configuration.AppSettingsReader().GetValue("MachineName", System.Type.GetType("System.String")).ToString() + ") is running above the threshold of " + Limit + "% space utilization.  You will want to correct this behavior quickly!\r\n\r\nThis is an automated message - have a great day!";

            /*
             * 
             * So, if you use the code as is (since I've enabled SSL), you'll get a Certificate Security Exception.
             * The correct solution is to do the following (so Mono can read the cert)...
             * 
             * mozroots --import --ask-remove --machine
             * certmgr -ssl smtps://smtp.gmail.com:465
             * 
             * But I'm going to just disable the above, since this is published publicly and will run for anybody that uses it straigt out of the box without having to add the certs.
             * Note that you shouldn't do this in production, and you'll want to change "IgnoreSSLCertificate" in my config file to "false"
             * 
             * If you fail do so, the program assumes it's talking to Gmail servers - which might not truely be the case if you don't validate Gmail certificate.
             * 
             */

            if (Convert.ToBoolean(new System.Configuration.AppSettingsReader().GetValue("IgnoreSSLCertificate", System.Type.GetType("System.Boolean"))))
            {
                //Setup a Fake Method to have a "I've received a valid certificate from Gmail" response to this program - it's a hack - read above for solution.
                System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate (object obj, System.Security.Cryptography.X509Certificates.X509Certificate cert, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors errors)
                {
                    return true;
                };
            }

            cli.Send(msg);
        }

        #region "Config File Password Encryption and Decryption Routines - Danger Will Robinson! Notice this not for security, just obscurity!"
        static private string DecryptPassword(string EncryptedPassword)
        {
            if (EncryptedPassword == null)
                return "";

            var base64EncodedBytes = System.Convert.FromBase64String(EncryptedPassword);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        static private string EncryptPassword(string DecryptedPassword)
        {
            if (DecryptedPassword == null)
                return "";

            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(DecryptedPassword);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        #endregion
    }
}
