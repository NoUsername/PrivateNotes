using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Mono.Unix;

namespace Tomboy.PrivateNotes
{
	public class PasswordEntry : Gtk.Dialog
	{
		AutoResetEvent autoReset;
		Gtk.Entry pw;
		Gtk.Entry pw2;
		Gtk.Label match_label;

		public PasswordEntry()
		{
			autoReset = new AutoResetEvent(false);
			Title = Catalog.GetString("Please enter the password");
			Gtk.VBox pwbox = new Gtk.VBox(false, 6);

			pwbox.PackStart(new Gtk.Label(Catalog.GetString("Please enter the password:")), true, true, 6);

			pw = new Gtk.Entry();
			// set password style:
			pw.InvisibleChar = '*';
			pw.Visibility = false;
			pw.Text = "";
			pwbox.PackStart(pw);

			pw2 = new Gtk.Entry();
			// set password style:
			pw2.InvisibleChar = '*';
			pw2.Visibility = false;
			pw2.Text = "";
			pwbox.PackStart(pw2);

			pw.Changed += PasswordChanged;
			pw2.Changed += PasswordChanged;

			match_label = new Gtk.Label();
			match_label.Markup = Catalog.GetString(AddinPreferences.MATCH_TEXT);
			pwbox.PackStart(match_label);


			Gtk.Button button = (Gtk.Button)AddButton(Gtk.Stock.Ok, Gtk.ResponseType.Ok);
			button.CanDefault = true;
			//button.Show();
			pwbox.PackStart(button);
			//this.VBox.PackStart(button);

			Gtk.AccelGroup accel_group = new Gtk.AccelGroup();
			AddAccelGroup(accel_group);

			button.AddAccelerator("activate",
														 accel_group,
														 (uint)Gdk.Key.Escape,
														 0,
														 0);

			AddActionWidget(button, Gtk.ResponseType.Ok);
			DefaultResponse = Gtk.ResponseType.Ok;

			accel_group.AccelActivate += OnAction;
			Response += OnResponse;

			DeleteEvent += new Gtk.DeleteEventHandler(PasswordEntry_DeleteEvent);

			pwbox.ShowAll();
			this.VBox.PackStart(pwbox);

			// show() must happen on ui thread
			Gtk.Application.Invoke(RunInUiThread);
		}

		/// <summary>
		/// used to show the dialog via the UI-Thread
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="ea"></param>
		public void RunInUiThread(object sender, EventArgs ea)
		{
			Present();
		}

		public void PasswordChanged(object sender, EventArgs args)
		{
			if (pw.Text.Equals(pw2.Text))
			{
				match_label.Markup = Catalog.GetString(AddinPreferences.MATCH_TEXT);
			}
			else
			{
				match_label.Markup = Catalog.GetString(AddinPreferences.MATCH_NOT_TEXT);
			}
		}


		/// <summary>
		/// waits for a correct password to be put in
		/// </summary>
		/// <param name="o"></param>
		/// <param name="args"></param>
		public void OnAction(object o, Gtk.AccelActivateArgs args)
		{
			if (pw.Text.Equals(pw2.Text))
			{
				// action
				autoReset.Set();
				Hide();
			}
		}

		// forward other events
		public void OnResponse(object o, Gtk.ResponseArgs args) {
			OnAction(null, null);
		}

		// forward other events
		// react to the [x]-button being pressed
		void PasswordEntry_DeleteEvent(object o, Gtk.DeleteEventArgs args)
		{
			OnAction(null, null);
		}

		public String getPassword()
		{
			String password = null;
			autoReset.WaitOne();
			password = pw.Text;

			// dispose the gui thigns
			pw.Text = "";
			pw2.Text = "";
			Destroy();

			return password;
		}

	}
}
