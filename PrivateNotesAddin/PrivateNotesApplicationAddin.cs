
using System;
using System.Runtime.InteropServices;

using Gtk;

using Tomboy;

namespace Tomboy.PrivateNotes
{
  public class PrivateNotesApplicationAddin : ApplicationAddin
	{
		bool initialized = false;
		bool timeout_owner;
		static InterruptableTimeout timeout;
		NoteManager manager;

		// Called only by instance with timeout_owner set.
		void CheckNewDay (object sender, EventArgs args)
		{
      Console.WriteLine("test...");

			// Re-run every minute
			timeout.Reset (1000 * 60);
		}

		public override void Initialize ()
		{
			if (timeout == null) {
				timeout = new InterruptableTimeout ();
				timeout.Timeout += CheckNewDay;
				timeout.Reset (0);
				timeout_owner = true;
			}
			manager = Tomboy.DefaultNoteManager;
			initialized = true;
		}

		public override void Shutdown ()
		{
			if (timeout_owner) {
				timeout.Timeout -= CheckNewDay;
				timeout.Cancel();
				timeout = null;
			}

			initialized = false;
		}

		public override bool Initialized
		{
			get {
				return initialized;
			}
		}
	}
}
