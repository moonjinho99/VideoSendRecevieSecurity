using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System.Security.Cryptography;

namespace VideoSendRecevieSecurity
{
    public class SeedCrypto
    {

        private const int KeySize = 16;
        private const int BlockSize = 16;

        public byte[] Encrypt(byte[] plainBytes, byte[] key, byte[] iv)
        {
            BufferedBlockCipher cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(new SeedEngine()));
            cipher.Init(true, new ParametersWithIV(new KeyParameter(key), iv));

            return CipherData(cipher, plainBytes);
        }

        public byte[] Decrypt(byte[] cipherBytes, byte[] key, byte[] iv)
        {
            BufferedBlockCipher cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(new SeedEngine()));
            cipher.Init(false, new ParametersWithIV(new KeyParameter(key), iv));

            return CipherData(cipher, cipherBytes);
        }

        private byte[] CipherData(BufferedBlockCipher cipher, byte[] input)
        {
            byte[] output = new byte[cipher.GetOutputSize(input.Length)];
            int length = cipher.ProcessBytes(input, 0, input.Length, output, 0);
            try
            {
                length += cipher.DoFinal(output, length);
            }
            catch (CryptoException e)
            {
                throw new IOException("암호화 에러 : " + e);
            }

            byte[] result = new byte[length];
            Array.Copy(output, 0, result, 0, length);
            return result;
        }

        public byte[] GenerateRandomBytes(int length)
        {
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] bytes = new byte[length];
                rng.GetBytes(bytes);
                return bytes;
            }
        }
    }
}
