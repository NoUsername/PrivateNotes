#define RANDOM_PADDING

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Tomboy.PrivateNotes
{

	/// <summary>
	/// util class
	/// filesystem and byte-conversion related helpers
	/// </summary>
  public class Util
  {
#if RANDOM_PADDING
    private static Random random = new Random();
#endif

		/// <summary>
		/// makes sure that a file exists
		/// </summary>
		/// <param name="_path"></param>
    public static void AssureFileExists(String _path)
    {
      if (!File.Exists(_path))
        File.Create(_path).Close();
    }

		/// <summary>
		/// deletes all files in a directory (not sub-directories!)
		/// </summary>
		/// <param name="_path"></param>
    public static void DelelteFilesInDirectory(String _path)
    {
      DirectoryInfo info = new DirectoryInfo(_path);
      foreach (FileInfo file in info.GetFiles())
      {
        file.Delete();
      }
    }

		/// <summary>
		/// convert from a unix timestamp to a c# dateTime object
		/// </summary>
		/// <param name="timestamp"></param>
		/// <returns></returns>
    public static DateTime ConvertFromUnixTimestamp(long timestamp)
    {
      DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
      return origin.AddSeconds(timestamp);
    }

		/// <summary>
		/// converts a c# dateTime object to a unix timestamp
		/// </summary>
		/// <param name="date"></param>
		/// <returns></returns>
    public static long ConvertToUnixTimestamp(DateTime date)
    {
      DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
      TimeSpan diff = date - origin;
      return (long)Math.Floor(diff.TotalSeconds);
    }

		/// <summary>
		/// string to bytes (to have one central place where the codepage is defined)
		/// </summary>
		/// <param name="_s"></param>
		/// <returns></returns>
    public static byte[] GetBytes(String _s)
    {
      return Encoding.UTF8.GetBytes(_s);
    }

		/// <summary>
		/// bytes to stirng (to have one central place where the codepage is defined)
		/// </summary>
		/// <param name="_data"></param>
		/// <returns></returns>
    public static String FromBytes(byte[] _data)
    {
      return Encoding.UTF8.GetString(_data);
    }

		/// <summary>
		/// check if 2 byte arrays are equal
		/// </summary>
		/// <param name="_array1"></param>
		/// <param name="_array2"></param>
		/// <returns></returns>
    public static bool ArraysAreEqual(byte[] _array1, byte[] _array2)
    {
      if (_array1 == null || _array2 == null)
        return false;
      if (_array1 == _array2)
        return true;
      if (_array1.Length != _array2.Length)
        return false;

      for (int i = 0; i < _array1.Length; i++)
        if (_array1[i] != _array2[i])
          return false;

      return true;
    }

		/// <summary>
		/// pad some byte-data to a certain length
		/// </summary>
		/// <param name="_data"></param>
		/// <param name="_multipleOf"></param>
		/// <returns></returns>
    public static byte[] padData(byte[] _data, int _multipleOf)
    {
      int tooMuch = _data.Length % _multipleOf;
      int padBytes = _multipleOf - tooMuch;
      byte[] newData = new byte[_data.Length + padBytes];
      System.Array.Copy(_data, newData, _data.Length);
#if RANDOM_PADDING
      // fill rest with random data
      byte[] randomPad = new byte[padBytes];
      random.NextBytes(randomPad);
      System.Array.Copy(randomPad, 0, newData, _data.Length, padBytes);
#endif
      return newData;
    }

    /// <summary>
    /// adds 4 byte length info at the beginning, supports max. length of the max value of int32
    /// </summary>
    /// <param name="_data"></param>
    /// <param name="_multipleOf"></param>
    /// <returns></returns>
    public static byte[] padWithLengthInfo(byte[] _data, int _multipleOf)
    {
      int tooMuch = (_data.Length + 4) % _multipleOf;
      int padBytes = _multipleOf - tooMuch;
      byte[] newData = new byte[_data.Length + padBytes + 4];
      if (_data.LongLength > Int32.MaxValue)
      {
        throw new InvalidOperationException("you can't use this much of data, because the length information only uses 4 bytes");
      }
      // get length info
      byte[] lengthInfo = System.BitConverter.GetBytes((int)_data.Length);
      // write length info
      System.Array.Copy(lengthInfo, 0, newData, 0, lengthInfo.Length);
      // write data
      System.Array.Copy(_data, 0, newData, 4, _data.Length);
#if RANDOM_PADDING
      // fill rest with random data
      byte[] randomPad = new byte[padBytes];
      random.NextBytes(randomPad);
      System.Array.Copy(randomPad, 0, newData, lengthInfo.Length + _data.Length, padBytes);
#endif
      return newData;
    }

    /// <summary>
    /// reads the first 4 bytes of an array, converts that to an int, and reads that many following bytes of
    /// the array and returns them
    /// </summary>
    /// <param name="_data"></param>
    /// <returns></returns>
    public static byte[] getDataFromPaddedWithLengthInfo(byte[] _data)
    {
      if (_data.Length < 4)
        throw new InvalidOperationException("the data must at least contain the length info");

      int lenghtInfo = BitConverter.ToInt32(_data, 0);
      if (_data.Length < 4 + lenghtInfo)
        throw new InvalidOperationException("length info invalid, array not long enough to hold that much data");

      byte[] realData = new byte[lenghtInfo];
      System.Array.Copy(_data, 4, realData, 0, lenghtInfo);
      return realData;
    }
  }

#if WIN32 && DPAPI
  // DPAPI stuff, only exists on windows:

  /// <summary>
  /// Windows Data Protection API. Data is protected in a way, that it is only
  /// accessible by the currently logged in user
  /// </summary>
  public class DPAPIUtil
  {

    /// <summary>
    /// stores the password in a protected file
    /// </summary>
    /// <param name="_pw"></param>
    public static void storePassword(String _pw)
    {
      String dataFile = getDataFilePath();
      byte[] toEncrypt = Util.GetBytes(_pw);
      byte[] encrypted = System.Security.Cryptography.ProtectedData.Protect(toEncrypt, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
     
      Util.AssureFileExists(dataFile);

      FileStream fout = File.OpenWrite(dataFile);
      fout.Write(encrypted, 0, encrypted.Length);
      fout.Close();
    }

    /// <summary>
    /// gets the password from the protected file
    /// </summary>
    /// <returns></returns>
    public static String getPassword()
    {
      byte[] todecrypt;
      try
      {
        String dataFile = getDataFilePath();
        FileStream fin = File.OpenRead(dataFile);
        {
          MemoryStream buf = new MemoryStream();
          int b = fin.ReadByte();
          while (b >= 0)
          {
            buf.WriteByte((byte)b);
            b = fin.ReadByte();
          }
          todecrypt = buf.ToArray();
        }
      }
      catch (Exception _e)
      {
        Logger.Info("Could not retrieve key from dpapi, maybe the file doesn't exist.", _e);
        return null;
      }

      byte[] decrypted = System.Security.Cryptography.ProtectedData.Unprotect(todecrypt, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);

      return Util.FromBytes(decrypted);
    }

    private static String getDataFilePath()
    {
      return Path.Combine(Services.NativeApplication.ConfigurationDirectory, "dpapi_file.dat");
    }

  }


#endif


	/// <summary>
	/// gtk helper
	/// some small useful helpers that make certain things easier with the gtk lib
	/// </summary>
  public class GtkUtil
  {
    public static void ShowHintWindow(Gtk.Widget parent, String caption, String text)
    {
      Gtk.Dialog dialog = new Gtk.Dialog();
      dialog.ParentWindow = parent.GdkWindow;
      dialog.Parent = parent;
      dialog.Title = caption;
      dialog.VBox.PackStart(new Gtk.Label(text), true, true, 12);
      
      Gtk.Button closeButton = (Gtk.Button)dialog.AddButton(Gtk.Stock.Ok, Gtk.ResponseType.Close);
      closeButton.Clicked += delegate(object sender, EventArgs ea) { dialog.Hide(); dialog.Dispose(); };

      EventHandler showDelegate = delegate(object s, EventArgs ea) { dialog.ShowAll(); dialog.Present(); };
      Gtk.Application.Invoke(showDelegate);
    }

    /// <summary>
    /// quick wrapper to simplify label creation when you need to set markup, not the text (because there is no such constructor)
    /// </summary>
    /// <param name="_markup"></param>
    /// <returns></returns>
    public static Gtk.Label newMarkupLabel(String _markup)
    {
      var l = new Gtk.Label();
      l.Markup = _markup;
      return l;
    }


  }
}
