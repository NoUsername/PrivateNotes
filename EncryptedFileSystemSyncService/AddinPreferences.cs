using System;
using Tomboy;
using Mono.Unix;

namespace Tomboy.PrivateNotesLocal
{
	public class AddinPreferences : Gtk.VBox
	{
		// PREFERENCE CONSTANTS:
		public const string SYNC_PRIVATENOTES_LOCAL_PASSWORD = "/apps/tomboy/sync/private_notes_local/password";
		public const string SYNC_PRIVATENOTES_LOCAL_ASKEVERYTIME = "/apps/tomboy/sync/private_notes_local/ask_for_password";

		/** texts for the math label */
		private const string MATCH_TEXT = "<markup><span foreground=\"green\">match</span></markup>";
		private const string MATCH_NOT_TEXT = "<span foreground=\"red\">don't match</span>";

		Gtk.RadioButton rbt_storePw;
		Gtk.RadioButton rbt_alwaysAsk;
		Gtk.Entry stored_pw;
		Gtk.Entry stored_pw2; // confirm
		Gtk.Label match_label;

		public AddinPreferences()
: base (false, 12)
		{
			Gtk.VBox container = new Gtk.VBox(false, 12);
			Gtk.HBox customBox = new Gtk.HBox(false, 12);
			PackStart(container);
			container.PackStart(customBox);

			rbt_storePw = new Gtk.RadioButton(Catalog.GetString("_Store password"));
			customBox.PackStart(rbt_storePw);

			customBox = new Gtk.HBox(false, 12);
			container.PackStart(customBox);

			//	--- Password Boxes --- 
			String pw = (string)Preferences.Get(SYNC_PRIVATENOTES_LOCAL_PASSWORD);
			pw = (pw == null) ? "" : pw;
			Gtk.VBox pwbox = new Gtk.VBox(false, 12);
			Gtk.HBox superbox = new Gtk.HBox(false, 12);
			superbox.PackStart(new Gtk.Alignment(0, 0, 50, 0)); // spacer
			superbox.PackStart(pwbox);
			customBox.PackStart(superbox);

			stored_pw = new Gtk.Entry();
			// set password style:
			stored_pw.InvisibleChar = '*';
			stored_pw.Visibility = false;
			stored_pw.Text = pw;
			pwbox.PackStart(stored_pw);

			stored_pw2 = new Gtk.Entry();
			// set password style:
			stored_pw2.InvisibleChar = '*';
			stored_pw2.Visibility = false;
			stored_pw2.Text = pw;
			pwbox.PackStart(stored_pw2);

			match_label = new Gtk.Label();
			match_label.Markup = MATCH_TEXT;
			pwbox.PackStart(match_label);

			//IPropertyEditor entryEditor = Services.Factory.CreatePropertyEditorEntry(
			//	SYNC_PRIVATENOTES_PASSWORD, stored_pw);
			//entryEditor.Setup();


			customBox = new Gtk.HBox(false, 12);
			container.PackStart(customBox);

			// give the first rbt here to link the 2
			rbt_alwaysAsk = new Gtk.RadioButton(rbt_storePw, Catalog.GetString("_Always ask for password"));
			customBox.PackStart(rbt_alwaysAsk);

			// assign event-listeners
			rbt_storePw.Toggled += PasswordMethodChanged;
			stored_pw.Changed += PasswordChanged;
			stored_pw2.Changed += PasswordChanged;


			Object value = Preferences.Get(SYNC_PRIVATENOTES_LOCAL_ASKEVERYTIME);
			if (value == null || value.Equals(false))
			{
				rbt_storePw.Active = true;
			}
			else
			{
				rbt_alwaysAsk.Active = true;
			}

			ShowAll ();
		}

		/// <summary>
		/// radiobutton changed
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		void PasswordMethodChanged (object sender, EventArgs args)
		{
			bool storedPwEnabled = rbt_storePw.Active;
			
			stored_pw.Sensitive = storedPwEnabled;
			stored_pw2.Sensitive = storedPwEnabled;
			match_label.Sensitive = storedPwEnabled;
			Preferences.Set(SYNC_PRIVATENOTES_LOCAL_ASKEVERYTIME, !rbt_storePw.Active);
		}

		/// <summary>
		/// entered a new password
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		void PasswordChanged(object sender, EventArgs args)
		{
			if (stored_pw.Text.Equals(stored_pw2.Text))
			{
				match_label.Markup = MATCH_TEXT;
				Preferences.Set(SYNC_PRIVATENOTES_LOCAL_PASSWORD, stored_pw.Text);
			}
			else
			{
				match_label.Markup = MATCH_NOT_TEXT;
			}
		}
	}
}
