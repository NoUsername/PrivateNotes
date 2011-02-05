using System;
using Tomboy;

namespace Tomboy.PrivateNotes
{
	public class AddinPreferencesFactory : AddinPreferenceFactory
	{
		public override Gtk.Widget CreatePreferenceWidget ()
		{
			return new AddinPreferences();
		}
	}
}
