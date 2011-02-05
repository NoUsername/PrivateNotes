using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace Tomboy.PrivateNotes.Crypto
{

	public interface CryptoFormat
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="_filename"></param>
		/// <param name="_content"></param>
		/// <param name="_key">the key that is should be used for encryption, it is already hashed (and was salted before)</param>
		/// <param name="_salt">the salt that was used to salt the key</param>
		/// <returns></returns>
		bool WriteCompatibleFile(String _filename, byte[] _content, byte[] _key, byte[] _salt);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="_filename"></param>
		/// <param name="fin"></param>
		/// <param name="_key">the unsalted and unhashed key</param>
		/// <param name="_wasOk"></param>
		/// <returns></returns>
		byte[] DecryptFromStream(String _filename, Stream fin, byte[] _key, out bool _wasOk);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="_filename"></param>
		/// <param name="_key">the unsalted and unhashed key</param>
		/// <param name="_wasOk"></param>
		/// <returns></returns>
		byte[] DecryptFile(String _filename, byte[] _key, out bool _wasOk);

		int Version();
	}

	/// <summary>
	/// factory for getting the format provider for a specified protocol version
	/// </summary>
	public class CryptoFormatProviderFactory
	{
		private static CryptoFormatProviderFactory SINGLETON = new CryptoFormatProviderFactory();

		public static CryptoFormatProviderFactory INSTANCE {
			get {
				return SINGLETON;
			}
		}

		private CryptoFormat defaultFormat = new CryptoFileFormatRev1();

		public CryptoFormat GetCryptoFormat()
		{
			return defaultFormat; 
		}

		public CryptoFormat GetCryptoFormat(int version)
		{
			// TODO implement stuff for different versions
			switch (version)
			{
				case 0:
					return new CryptoFileFormatRev0();

				case 1:
					return defaultFormat;

				default:
					throw new Exception("Unknown encryption version");
					//break; // not needed because of throw
			}

			return defaultFormat;
		}
		

	}

	


	public class AESUtil
	{
		private const int SALT_SIZE = 32;
		private static Random random_source = new Random();

		public static byte[] CalculateHash(byte[] _data)
		{
			return System.Security.Cryptography.SHA256.Create().ComputeHash(_data);
		}

		/// <summary>
		/// hashes the password with a random salt that is generated before
		/// </summary>
		/// <param name="_data">the data to hash</param>
		/// <param name="_salt">the salt that was used</param>
		/// <returns>the salted hash</returns>
		public static byte[] CalculateSaltedHash(byte[] _data, out byte[] _salt)
		{
			byte[] toHash = new byte[_data.Length + SALT_SIZE];
			_salt = new byte[SALT_SIZE];
			random_source.NextBytes(_salt);
			return CalculateSaltedHash(_data, _salt);
		}

		/// <summary>
		/// hashes the password with a the given salt
		/// </summary>
		/// <param name="_data">the data to hash</param>
		/// <param name="_salt">the salt to use</param>
		/// <returns>the salted hash</returns>
		public static byte[] CalculateSaltedHash(byte[] _data, byte[] _salt)
		{
			byte[] toHash = new byte[_data.Length + SALT_SIZE];
			Array.Copy(_data, toHash, _data.Length);
			Array.Copy(_salt, 0, toHash, _data.Length, SALT_SIZE);
			byte[] result = System.Security.Cryptography.SHA256.Create().ComputeHash(toHash);
			Array.Clear(toHash, 0, toHash.Length);
			return result;
		}

		public static void Encrypt(byte[] _key, byte[] _data, String _toFile)
		{
			byte[] encrypted = Encrypt(_key, _data);

			Util.AssureFileExists(_toFile);
			FileStream fout = new FileStream(_toFile, FileMode.Truncate, FileAccess.Write);

			fout.Write(encrypted, 0, encrypted.Length);
			fout.Close();
		}

		/// <summary>
		/// encrypt data with Rijndawel in CBC mode (if length > 16 bytes, else EBC mode)
		/// </summary>
		/// <param name="_key">the key to use</param>
		/// <param name="_data">the data to encrypt, length must be % 16 == 0</param>
		/// <returns>the encrypted data, if more than 16 bytes were passed in, the IV will be at the first 16 bytes</returns>
		public static byte[] Encrypt(byte[] _key, byte[] _data)
		{
			System.Diagnostics.Debug.Assert(_data.Length % 16 == 0, "invalid data length " + _data.Length);

			Rijndael rij = Rijndael.Create();
			rij.Key = _key;
			rij.Padding = PaddingMode.None;
			byte[] iv = rij.IV;
			int resultLenght = _data.Length;
			if (_data.Length == 16)
			{
				rij.Mode = CipherMode.ECB;
			}
			else
			{
				rij.Mode = CipherMode.CBC;
				resultLenght += rij.IV.Length;
			}

			ICryptoTransform ic = rij.CreateEncryptor();
			System.Diagnostics.Debug.Assert(ic.InputBlockSize == 16 && ic.OutputBlockSize == 16, "invalid blocksize");

			MemoryStream mem = new MemoryStream(resultLenght);
			if (rij.Mode != CipherMode.ECB)
			{
				// write iv first:
				mem.Write(iv, 0, iv.Length);
			}

			CryptoStream crypt = new CryptoStream(mem, ic, CryptoStreamMode.Write);
			crypt.Write(_data, 0, _data.Length);
			crypt.Close();
			return mem.ToArray();
		}

		public static byte[] Decrypt(byte[] _key, String _fromFile)
		{
			FileStream fin = new FileStream(_fromFile, FileMode.Open, FileAccess.Read);
			MemoryStream mem = new MemoryStream();
			int read = fin.ReadByte();
			while (read >= 0)
			{
				mem.WriteByte((byte)read);
				read = fin.ReadByte();
			}

			return Decrypt(_key, mem.ToArray());
		}

		/// <summary>
		/// decrypt data with Rijndawel in CBC mode (if length > 16 bytes, else EBC mode)
		/// </summary>
		/// <param name="_key">the key to use</param>
		/// <param name="_data">the data to decrypt, if length > 16 bytes, the IV has to be stored in the first 16 bytes, length must be % 16 == 0</param>
		/// <returns>the decrypted data</returns>
		public static byte[] Decrypt(byte[] _key, byte[] _data)
		{
			System.Diagnostics.Debug.Assert(_data.Length % 16 == 0);
			MemoryStream mem = new MemoryStream(_data);
			Rijndael rij = Rijndael.Create();
			rij.Key = _key;
			rij.Padding = PaddingMode.None;
			byte[] result = null;
			if (mem.Length == 16)
			{
				result = new byte[mem.Length];
				rij.Mode = CipherMode.ECB;
			}
			else
			{
				result = new byte[mem.Length - 16];
				// read iv from beginning of stream
				byte[] iv = new byte[16];
				mem.Read(iv, 0, iv.Length);

				rij.Mode = CipherMode.CBC;
				rij.IV = iv;
			}

			ICryptoTransform ic = rij.CreateDecryptor();
			System.Diagnostics.Debug.Assert(ic.InputBlockSize == 16 && ic.OutputBlockSize == 16, "invalid blocksize");

			CryptoStream crypt = new CryptoStream(mem, ic, CryptoStreamMode.Read);
			crypt.Read(result, 0, result.Length);
			crypt.Close();
			return result;
		}

	}

	/// <summary>
	/// exceptions with encryption
	/// </summary>
	public class EncryptionException : Exception {

		public EncryptionException(String _msg) : base(_msg)
		{
			
		}

	}

	/// <summary>
	/// exceptions concerning the password (most likely: the wrong password was used)
	/// </summary>
	public class PasswordException : Exception
	{

		public PasswordException(String _msg)
			: base(_msg)
		{

		}

	}


}
