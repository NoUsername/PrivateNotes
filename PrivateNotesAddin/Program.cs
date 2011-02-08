using System;
using System.Collections.Generic;
using System.Text;
using Tomboy.PrivateNotes.Crypto;
using Tomboy.Sync;
using Tomboy.PrivateNotes;

namespace PrivateNotes
{
  class Program
  {

		/// <summary>
		/// for testing purposes. this is of course useless when compiled as .dll
		/// left in here for quick experiments/tests
		/// </summary>
		/// <param name="args"></param>
    public static void Main(String[] args)
    {
      SecurityWrapper.CopyAndEncrypt(@"Z:\out.txt", @"Z:\out_enc.txt", Util.GetBytes("1234567890123456"));
      Console.ReadKey();
    }
  }
}
