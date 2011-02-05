using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Tomboy.PrivateNotes.Crypto;
using Tomboy.PrivateNotes;

namespace Tomboy.Sync
{
	/// <summary>
	/// security wrapper for the local encrypted FileSystemSynchronization
	/// </summary>
	class SecurityWrapper
	{

		public static void CopyAndEncrypt(String _inputFile, String _toFile, byte[] _password)
		{

			FileStream input = File.OpenRead(_inputFile);
			MemoryStream membuf = new MemoryStream();
			int b = input.ReadByte();
			while (b >= 0)
			{
				membuf.WriteByte((byte)b);
				b = input.ReadByte();
			}
			input.Close();

			byte[] salt;
			byte[] key = AESUtil.CalculateSaltedHash(_password, out salt);

			CryptoFormat ccf = CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat();
			ccf.WriteCompatibleFile(_toFile, membuf.ToArray(), key, salt);
		}

		public static void SaveAsEncryptedFile(String _fileName, byte[] _data, byte[] _password)
		{
			CryptoFormat ccf = CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat();

			byte[] salt;
			byte[] key = AESUtil.CalculateSaltedHash(_password, out salt);
			ccf.WriteCompatibleFile(_fileName, _data, key, salt);
		}

		public static Stream DecryptFromStream(String _inputFile, Stream _s, byte[] _key, out bool	_wasOk)
		{
			CryptoFormat ccf = CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat();
			byte[] data = ccf.DecryptFromStream(_inputFile, _s, _key, out _wasOk);
			if (!_wasOk)
				return null;

			return new MemoryStream(data);
		}



	}
}
