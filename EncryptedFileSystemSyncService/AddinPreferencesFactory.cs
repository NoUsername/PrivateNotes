using System;
using Tomboy;

namespace Tomboy.PrivateNotesLocal
{
	public class AddinPreferencesFactory : AddinPreferenceFactory
	{
		public override Gtk.Widget CreatePreferenceWidget ()
		{
			return new AddinPreferences();
		}
	}
}
