//#define USE_LOCAL_TEST

using System;
using System.IO;

using Gtk;
using Mono.Unix;

using Tomboy;
using Tomboy.PrivateNotes;
using Tomboy.PrivateNotes.Crypto;

/*! \mainpage PrivateNotes TomboyAddin Start Page
 *
 * \section intro_sec Introduction
 *
 * Welcome to the PrivateNotes TomboyAddin documentation. <br/>
 * 
 * Here you find the extracted code-documentation. To get more information/stuff go
 * to <a href="http://privatenotes.dyndns-server.com/wiki/">our project website
 * privatenotes.dyndns-server.com/wiki/</a>.
 *
 * \section start_sec Getting started
 *
 * If you want to get started reading some documentation a good starting point might
 * be Tomboy.PrivateNotes.EncryptedWebdavSyncServer or more specifically the "Member List"
 * on that page.
 *  
 */
namespace Tomboy.Sync
{

	/// <summary>
	/// the actual addin class.
	/// responsible for creating the sync server object and handling the
	/// sync-preferences gui (sync tab in tomboy prefs)
	/// </summary>
	public class EncryptedWebdavSyncServiceAddin : SyncServiceAddin
	{
		// TODO: Extract most of the code here and build GenericSyncServiceAddin
		// that supports a field, a username, and password.	This could be useful
		// in quickly building SshSyncServiceAddin, FtpSyncServiceAddin, etc.

		#region GUI ELEMENTS
		//private FileChooserButton pathButton;
		private Gtk.RadioButton rbt_storePw;
		private Gtk.RadioButton rbt_alwaysAsk;
		private Gtk.Entry stored_pw;
		private Gtk.Entry stored_pw2; // confirm
		private Gtk.Label match_label;
		private Gtk.CheckButton check_ssl;

		private Gtk.Entry server_path;
		private Gtk.Entry server_user;
		private Gtk.Entry server_pass;
		#endregion

		private bool initialized = false;

		/// <summary>
		/// Called as soon as Tomboy needs to do anything with the service
		/// </summary>
		public override void Initialize ()
		{
			initialized = true;
		}

		public override void Shutdown ()
		{
			// Do nothing for now
		}

		public override bool Initialized {
			get {
				return initialized;
			}
		}


		/// <summary>
		/// Creates a SyncServer instance that the SyncManager can use to
		/// synchronize with this service.	This method is called during
		/// every synchronization process.	If the same SyncServer object
		/// is returned here, it should be reset as if it were new.
		/// </summary>
		public override SyncServer CreateSyncServer ()
		{
			SyncServer server = null;

			String password;
			WebDAVInterface webdavserver;


			if (GetConfigSettings(out password, out webdavserver))
			{
				try
				{
					server = new EncryptedWebdavSyncServer(Services.NativeApplication.CacheDirectory, Util.GetBytes(password), webdavserver);
				}
				catch (PasswordException)
				{
					// Display window with hint that the pw is wrong
					GtkUtil.ShowHintWindow(Tomboy.SyncDialog, "Wrong Password", "The password you provided was wrong.");
					throw;
				}
				catch (FormatException)
				{
					// Display window with hint
					GtkUtil.ShowHintWindow(Tomboy.SyncDialog, "Encryption Error", "The encrypted files seem to be corrupted.");
					throw;
				}
				catch (WebDavException wde)
				{
					Exception inner = wde.InnerException;
					for (int i = 0; i < 10 && (inner.InnerException != null); i++) // max 10
						inner = inner.InnerException;

					GtkUtil.ShowHintWindow(Tomboy.SyncDialog, "WebDav Error", "Error while communicating with server:\n" + inner.Message);
					throw;
				}
			} else {
				throw new InvalidOperationException ("FileSystemSyncServiceAddin.CreateSyncServer () called without being configured");
			}

			return server;
		}

		public override void PostSyncCleanup ()
		{
			// Nothing to do

		}

		/// <summary>
		/// Creates a Gtk.Widget that's used to configure the service.	This
		/// will be used in the Synchronization Preferences.	Preferences should
		/// not automatically be saved by a GConf Property Editor.	Preferences
		/// should be saved when SaveConfiguration () is called.
		/// </summary>
		public override Gtk.Widget CreatePreferencesControl ()
		{
			Gtk.VBox container = new Gtk.VBox(false, 0);

			container.PackStart(GtkUtil.newMarkupLabel(Catalog.GetString("<span weight='bold'>Server Settings:</span>")));
			SetupGuiServerRelated(container, 4);
			container.PackStart(new Gtk.Label());
			container.PackStart(GtkUtil.newMarkupLabel(Catalog.GetString("<span weight='bold'>Encryption Settings:</span>")));
			SetupGuiEncryptionRelated(container, 4);
			container.ShowAll();
			return container;
		}

		/// <summary>
		/// The Addin should verify and check the connection to the service
		/// when this is called.	If verification and connection is successful,
		/// the addin should save the configuration and return true.
		/// </summary>
		public override bool SaveConfiguration ()
		{
			string serverPath = server_path.Text.Trim();

			if (serverPath.Trim().Equals(String.Empty)) {
				// TODO: Figure out a way to send the error back to the client
				Logger.Debug ("The serverpath is empty");
				throw new TomboySyncException (Catalog.GetString ("Folder path field is empty."));
			}

			if (!stored_pw.Text.Equals(stored_pw2.Text)) {
				Logger.Debug ("Passwords must match!");
				throw new TomboySyncException (Catalog.GetString ("Passwords must match!"));
			}

			// actually save if everything was ok
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_ASKEVERYTIME, (bool)!rbt_storePw.Active);
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SERVERPATH, (string)serverPath);
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SERVERUSER, (string)server_user.Text);
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SERVERPASS, (string)server_pass.Text);
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SERVERCHECKSSLCERT, (bool)check_ssl.Active);

			if (rbt_storePw.Active)
				storePassword(stored_pw.Text);
			else
			{
				// don't store, delete it from prefs and gui (to make it clear to the user
				stored_pw.Text = string.Empty;
				stored_pw2.Text = string.Empty;
				storePassword(string.Empty);
			}

			return true;
		}

		/// <summary>
		/// Reset the configuration so that IsConfigured will return false.
		/// </summary>
		public override void ResetConfiguration ()
		{
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_ASKEVERYTIME, "true");
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SERVERPASS, string.Empty);
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SERVERPATH, string.Empty);
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SERVERUSER, string.Empty);
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SERVERCHECKSSLCERT, "true");
			storePassword(" ");
		}

		/// <summary>
		/// Returns whether the addin is configured enough to actually be used.
		/// </summary>
		public override bool IsConfigured
		{
			get {
				string syncPath = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERPATH) as String;

				if (syncPath != null && syncPath.Trim() != string.Empty) {
					return true;
				}

				return false;
			}
		}

		/// <summary>
		/// The name that will be shown in the preferences to distinguish
		/// between this and other SyncServiceAddins.
		/// </summary>
		public override string Name
		{
			get {
				return Mono.Unix.Catalog.GetString ("Encrypted WebDav Sync");
			}
		}

		/// <summary>
		/// Specifies a unique identifier for this addin.	This will be used to
		/// set the service in preferences.
		/// </summary>
		public override string Id
		{
			get {
				return "securewebdav";
			}
		}

		/// <summary>
		/// Returns true if the addin has all the supporting libraries installed
		/// on the machine or false if the proper environment is not available.
		/// If false, the preferences dialog will still call
		/// CreatePreferencesControl () when the service is selected.	It's up
		/// to the addin to present the user with what they should install/do so
		/// IsSupported will be true.
		/// </summary>
		public override bool IsSupported
		{
			get {
				return true;
			}
		}

		#region Private Methods

		/// <summary>
		/// store the password with the according method
		/// </summary>
		/// <param name="_pw"></param>
		private void storePassword(String _pw)
		{
#if WIN32 && DPAPI
				DPAPIUtil.storePassword(_pw);
#else
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_PASSWORD, _pw);
#endif
		}

		/// <summary>
		/// Get config settings
		/// </summary>
		private bool GetConfigSettings (out string _password, out WebDAVInterface _webdav)
		{
			_password = null;
			_webdav = null;

			object ask = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_ASKEVERYTIME);

			if (ask == null)
				return false;

			if (((bool)ask == false))
			{
#if WIN32 && DPAPI
				object pw = DPAPIUtil.getPassword();
#else
				object pw = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_PASSWORD);
#endif
				if (pw != null)
				{
					_password = Convert.ToString(pw); // quick fix -> a num-only pw is returned as an int o.O
				}
			}

			if (_password == null)
			{
				// ask for password
				var entryWindow = new PrivateNotes.PasswordEntry();

				_password = entryWindow.getPassword();
			}

#if USE_LOCAL_TEST
			 _webdav = new WebDAVInterface("http://localhost", "/webdav/notes", "wampp", "xampp", false);
#else
			Uri serverUri = new Uri(Convert.ToString(Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERPATH)));

			String serverHost = serverUri.GetLeftPart(UriPartial.Authority);
			String serverBasePath = serverUri.AbsolutePath;

			bool checkSslCertificates = true;
			object checksslobj = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERCHECKSSLCERT);
			if (checksslobj != null && (checksslobj.Equals(false) || checksslobj.Equals("false")))
				checkSslCertificates = false;

			string serverUser = (string)Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERUSER);
			string serverPass = (string)Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERPASS);

			Logger.Debug("will user server: " + serverHost + " path: " + serverBasePath);
			//Logger.Debug("creating server with user " + serverUser + " pass: " + serverPass);

			_webdav = new WebDAVInterface(serverHost, serverBasePath,
				serverUser,
				serverPass,
				checkSslCertificates);
#endif

			if (_webdav != null && _password != null)
				return true;

			return false;
		}
		#endregion // Private Methods

		#region Gui Setup Methods

		/// <summary>
		/// setup fields like: store password:yes/no and the actual password entry,
		/// if it should be stored
		/// </summary>
		/// <param name="insertTo"></param>
		/// <param name="defaultSpacing"></param>
		void SetupGuiEncryptionRelated(Gtk.Box insertTo, int defaultSpacing)
		{
			Gtk.HBox customBox = new Gtk.HBox(false, defaultSpacing);
			insertTo.PackStart(customBox);
			rbt_storePw = new Gtk.RadioButton(Catalog.GetString("_Store password"));
			customBox.PackStart(rbt_storePw);

			customBox = new Gtk.HBox(false, defaultSpacing);
			insertTo.PackStart(customBox);

			//	--- Password Boxes --- 
#if WIN32 && DPAPI
			String pw = DPAPIUtil.getPassword();
#else
			String pw = Convert.ToString(Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_PASSWORD));
#endif
			pw = (pw == null) ? "" : pw;
			Gtk.VBox pwbox = new Gtk.VBox(false, defaultSpacing);
			Gtk.HBox superbox = new Gtk.HBox(false, defaultSpacing);
			superbox.PackStart(new Gtk.Alignment(0, 0, 200, 0)); // spacer
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
			match_label.Markup = Catalog.GetString(AddinPreferences.MATCH_TEXT);
			pwbox.PackStart(match_label);

			customBox = new Gtk.HBox(false, defaultSpacing);
			insertTo.PackStart(customBox);

			// give the first rbt here to link the 2
			rbt_alwaysAsk = new Gtk.RadioButton(rbt_storePw, Catalog.GetString("_Always ask for password"));
			customBox.PackStart(rbt_alwaysAsk);

			
			// assign event-listener
			rbt_storePw.Toggled += PasswordMethodChanged;

			// init with values from preferences
			object value = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_ASKEVERYTIME);
			if (value == null || value.Equals(false))
			{
				rbt_storePw.Active = true;
			}
			else
			{
				rbt_alwaysAsk.Active = true;
			}

			// assign event-listeners
			stored_pw.Changed += PasswordChanged;
			stored_pw2.Changed += PasswordChanged;
		}

		/// <summary>
		/// server gui stuff:
		/// server path
		/// server username + password
		/// check server ssl certificate yes/no
		/// </summary>
		/// <param name="insertTo"></param>
		/// <param name="defaultSpacing"></param>
		void SetupGuiServerRelated(Gtk.Box insertTo, int defaultSpacing)
		{
			Gtk.Table customBox = new Gtk.Table(3, 2, false);

			// somehow you can't change the default spacing or set it for all rows
			for (int i = 0; i < 3; i++)
				customBox.SetRowSpacing((uint)i, (uint)defaultSpacing);

			// insert the labels
			customBox.Attach(new Gtk.Label(Catalog.GetString("Server path:")), 0, 1, 0, 1);
			customBox.Attach(new Gtk.Label(Catalog.GetString("Username:")), 0, 1, 1, 2);
			customBox.Attach(new Gtk.Label(Catalog.GetString("Password:")), 0, 1, 2, 3);

			insertTo.PackStart(customBox);
			server_path = new Gtk.Entry();
			customBox.Attach(server_path, 1, 2, 0, 1);
			string serverPath = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERPATH) as String;
			server_path.Text = serverPath;
			// NO EDITOR! because we only save when "SaveConfiguration" is called
			//IPropertyEditor serverEditor = Services.Factory.CreatePropertyEditorEntry(
			//	AddinPreferences.SYNC_PRIVATENOTES_SERVERPATH, server_path);
			//serverEditor.Setup();

			server_user = new Gtk.Entry();
			customBox.Attach(server_user, 1, 2, 1, 2);
			string serverUser = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERUSER) as String;
			server_user.Text = serverUser;
			// NO EDITOR! because we only save when "SaveConfiguration" is called
			//IPropertyEditor userEditor = Services.Factory.CreatePropertyEditorEntry(
			// AddinPreferences.SYNC_PRIVATENOTES_SERVERUSER, server_user);
			//userEditor.Setup();

			server_pass = new Gtk.Entry();
			server_pass.InvisibleChar = '*';
			server_pass.Visibility = false;
			customBox.Attach(server_pass, 1, 2, 2, 3);
			string serverpass = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERPASS) as String;
			server_pass.Text = serverpass;
			// NO EDITOR! because we only save when "SaveConfiguration" is called
			//IPropertyEditor passEditor = Services.Factory.CreatePropertyEditorEntry(
			// AddinPreferences.SYNC_PRIVATENOTES_SERVERPASS, server_pass);
			//passEditor.Setup();

			check_ssl = new Gtk.CheckButton(Catalog.GetString("Check servers SSL certificate"));
			insertTo.PackStart(check_ssl);

			// set up check-ssl certificate stuff
			object value = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERCHECKSSLCERT);
			if (value == null || value.Equals(true))
				check_ssl.Active = true;
		}

#endregion

		#region Gui Callbacks

		 /// <summary>
		/// radiobutton changed
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		void PasswordMethodChanged(object sender, EventArgs args)
		{
			bool storedPwEnabled = rbt_storePw.Active;

			stored_pw.Sensitive = storedPwEnabled;
			stored_pw2.Sensitive = storedPwEnabled;
			match_label.Sensitive = storedPwEnabled;
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
				match_label.Markup = Catalog.GetString(AddinPreferences.MATCH_TEXT);
			}
			else
			{
				match_label.Markup = Catalog.GetString(AddinPreferences.MATCH_NOT_TEXT);
			}
		}


		#endregion
	}
}
