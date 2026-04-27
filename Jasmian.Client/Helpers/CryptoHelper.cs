using System;
using System.IO;
using System.Security.Cryptography;

namespace Jasmian.Client.Helpers
{
    public static class CryptoHelper
    {
                        public static (string PublicKey, string PrivateKey) GenerateRSAKeys()
        {
            using (var rsa = RSA.Create(2048))             {
                                string publicKey = rsa.ToXmlString(false);
                string privateKey = rsa.ToXmlString(true);
                return (publicKey, privateKey);
            }
        }

                public static string EncryptMessage(string plainText, string receiverPublicKey)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using (var aes = Aes.Create())
            {
                                aes.GenerateKey();
                aes.GenerateIV();

                byte[] encryptedText;

                                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                    encryptedText = ms.ToArray();
                }

                byte[] encryptedAesKey;

                                using (var rsa = RSA.Create())
                {
                    rsa.FromXmlString(receiverPublicKey);
                    encryptedAesKey = rsa.Encrypt(aes.Key, RSAEncryptionPadding.Pkcs1);
                }

                                using (var finalStream = new MemoryStream())
                using (var bw = new BinaryWriter(finalStream))
                {
                    bw.Write(encryptedAesKey.Length);
                    bw.Write(encryptedAesKey);
                    bw.Write(aes.IV.Length);
                    bw.Write(aes.IV);
                    bw.Write(encryptedText);

                                        return Convert.ToBase64String(finalStream.ToArray());
                }
            }
        }

                public static string DecryptMessage(string encryptedPackageBase64, string myPrivateKey)
        {
            if (string.IsNullOrEmpty(encryptedPackageBase64)) return encryptedPackageBase64;

            try
            {
                byte[] package = Convert.FromBase64String(encryptedPackageBase64);

                using (var ms = new MemoryStream(package))
                using (var br = new BinaryReader(ms))
                {
                                        int aesKeyLength = br.ReadInt32();
                    byte[] encryptedAesKey = br.ReadBytes(aesKeyLength);

                    int ivLength = br.ReadInt32();
                    byte[] iv = br.ReadBytes(ivLength);

                                        byte[] encryptedText = br.ReadBytes((int)(ms.Length - ms.Position));

                                        byte[] decryptedAesKey;
                    using (var rsa = RSA.Create())
                    {
                        rsa.FromXmlString(myPrivateKey);
                        decryptedAesKey = rsa.Decrypt(encryptedAesKey, RSAEncryptionPadding.Pkcs1);
                    }

                                        using (var aes = Aes.Create())
                    {
                        aes.Key = decryptedAesKey;
                        aes.IV = iv;

                        using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                        using (var cryptoStream = new MemoryStream(encryptedText))
                        using (var cs = new CryptoStream(cryptoStream, decryptor, CryptoStreamMode.Read))
                        using (var sr = new StreamReader(cs))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }
            catch
            {
                                return "[ 🔒 Сообщение не может быть расшифровано ]";
            }
        }
    }
}