// 
//  CommandClientTests.cs
//  
//  Author:
//       Levi Bard <levi@unity3d.com>
//  
//  Copyright (c) 2011 Levi Bard
// 
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using Xunit;

namespace Mercurial.Client.Tests
{
	public class CommandClientTests : IDisposable
	{
		static readonly string TestRepo = "https://bitbucket.org/TakUnity/monodevelop-hg";
		static List<string> garbage = new List<string> ();
		static readonly string MercurialPath = "hg";
		
		[DllImport ("libc")]
		static extern int uname (IntPtr buf);
		
		// From Managed.Windows.Forms/XplatUI
		static bool IsRunningOnMac ()
		{
			if (IsRunningOnWindows ()) return false;
			
			IntPtr buf = IntPtr.Zero;
			try {
				buf = Marshal.AllocHGlobal (8192);
				// This is a hacktastic way of getting sysname from uname ()
				if (uname (buf) == 0) {
					string os = Marshal.PtrToStringAnsi (buf);
					return (os == "Darwin");
				}
			} catch (Exception ex) {
				Console.WriteLine (ex);
			} finally {
				if (buf != IntPtr.Zero)
					Marshal.FreeHGlobal (buf);
			}
			
			return false;
		}
		
		static bool IsRunningOnWindows ()
		{
			return (Path.DirectorySeparatorChar == '\\' && Environment.NewLine == "\r\n");
		}
				
		public void Dispose()
		{
			foreach (string garbageDir in garbage) {
				try {
					Directory.Delete (garbageDir, true);
				} catch {
					// Don't care
				}
			}
		}
		
		[Fact]
		public void TestConnection ()
		{
			string path = GetTemporaryPath ();
			CommandClient.Initialize (path, MercurialPath);
		}
		
		[Fact]
		public void TestConfiguration ()
		{
			string path = GetTemporaryPath ();
			CommandClient.Initialize (path, MercurialPath);
			
			using (CommandClient client = new CommandClient (path, null, null, MercurialPath)) {
				IDictionary<string,string > config = client.Configuration;
				Assert.NotNull (config);
				Assert.True(config.Count > 0, "Expecting nonempty configuration");
				// Console.WriteLine (config.Aggregate (new StringBuilder (), (s,pair) => s.AppendFormat ("{0} = {1}\n", pair.Key, pair.Value), s => s.ToString ()));
			}
		}
		
		[Fact]
		public void TestInitialize ()
		{
			string path = GetTemporaryPath ();
			CommandClient.Initialize (path, MercurialPath);
			Assert.True(Directory.Exists (Path.Combine (path, ".hg")), string.Format ("Repository was not created at {0}", path));
		}
		
		[Fact]
		public void TestRoot ()
		{
			string path = GetTemporaryPath ();
			CommandClient.Initialize (path, MercurialPath);
			Assert.True (Directory.Exists (Path.Combine (path, ".hg")), string.Format ("Repository was not created at {0}", path));

            using (var client = new CommandClient(path, null, null, MercurialPath))
            {
                Assert.Equal(path, client.Root);
            }
		}
		
		[Fact(Skip = "Don't thrash bitbucket")]
		public void TestCloneRemote ()
		{
			string path = GetTemporaryPath ();
			CommandClient.Clone (TestRepo, path, true, null, "10", null, false, true, MercurialPath);
			Assert.True (Directory.Exists (Path.Combine (path, ".hg")), string.Format ("Repository was not cloned from {0} to {1}", TestRepo, path));
		}
		
		[Fact]
		public void TestCloneLocal ()
		{
			string firstPath = GetTemporaryPath ();
			string secondPath = GetTemporaryPath ();
			string file = Path.Combine (firstPath, "foo");
			CommandClient.Initialize (firstPath, MercurialPath);
			
			using (var client = new CommandClient (firstPath, null, null, MercurialPath)) {
				File.WriteAllText (file, "1");
				client.Add (file);
				client.Commit ("1");
			}
			try {
			CommandClient.Clone (source: firstPath, destination: secondPath, mercurialPath: MercurialPath);
			} catch (Exception ex) {
				Console.WriteLine (ex);
				Assert.True (false, ex.Message);
			}
			Assert.True (Directory.Exists (Path.Combine (secondPath, ".hg")), string.Format ("Repository was not cloned from {0} to {1}", firstPath, secondPath));
			Assert.True (File.Exists (Path.Combine (secondPath, "foo")), "foo doesn't exist in cloned working copy");

            using (var client = new CommandClient(secondPath, null, null, MercurialPath))
            {
                IList<Revision> log = client.Log(null);
                Assert.Equal(1, log.Count);
            }
		}
		
		[Fact]
		public void TestAdd ()
		{
			string path = GetTemporaryPath ();
			IDictionary<string,Status > statuses = null;
			
			CommandClient.Initialize (path, MercurialPath);
			using (var client = new CommandClient (path, null, null, MercurialPath)) {
				File.WriteAllText (Path.Combine (path, "foo"), string.Empty);
				File.WriteAllText (Path.Combine (path, "bar"), string.Empty);
				client.Add (Path.Combine (path, "foo"), Path.Combine (path, "bar"));
				statuses = client.Status (null);
			}
			
			Assert.NotNull (statuses);
			Assert.True (statuses.ContainsKey ("foo"), "No status received for foo");
			Assert.True (statuses.ContainsKey ("bar"), "No status received for bar");
			Assert.Equal(FileStatus.Added, statuses["foo"]);
			Assert.Equal(FileStatus.Added, statuses["bar"]);
		}
		
		[Fact]
		public void TestCommit ()
		{
			string path = GetTemporaryPath ();
			CommandClient.Initialize (path, MercurialPath);
			using (var client = new CommandClient (path, null, null, MercurialPath)) {
				File.WriteAllText (Path.Combine (path, "foo"), string.Empty);
				File.WriteAllText (Path.Combine (path, "bar"), string.Empty);
				client.Add (Path.Combine (path, "foo"));
				client.Commit ("Commit all");
				Assert.True (!client.Status ().ContainsKey ("foo"), "Default commit failed for foo");
				
				File.WriteAllText (Path.Combine (path, "foo"), "foo");
				client.Add (Path.Combine (path, "bar"));
				client.Commit ("Commit only bar", Path.Combine (path, "bar"));
				Assert.True (!client.Status ().ContainsKey ("bar"), "Commit failed for bar");
				Assert.True (client.Status ().ContainsKey ("foo"), "Committed unspecified file!");
				Assert.Equal (FileStatus.Modified, client.Status ()["foo"]);
				Assert.Equal (2, client.Log (null).Count);
			}
		}
		
		[Fact]
		public void TestLog ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			CommandClient.Initialize (path, MercurialPath);
			
			using (var client = new CommandClient (path, null, null, MercurialPath)) {
				File.WriteAllText (file, "1");
				client.Add (file);
				client.Commit ("1");
				File.WriteAllText (file, "2");
				client.Commit ("2");
				Assert.Equal(2, client.Log (null).Count);
			}
		}
		
		[Fact]
		public void TestAnnotate ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			CommandClient.Initialize (path, MercurialPath);
			
			using (var client = new CommandClient (path, null, null, MercurialPath)) {
				File.WriteAllText (file, "1");
				client.Add (file);
				client.Commit ("1", null, false, false, null, null, null, null, "user");
				Assert.Equal("user 0: 1\n", client.Annotate (null, file));
			}
		}
		
		[Fact]
		public void TestDiff ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			string diffText = string.Empty;
			CommandClient.Initialize (path, MercurialPath);
			
			using (var client = new CommandClient (path, null, null, MercurialPath)) {
				File.WriteAllText (file, "1\n");
				client.Add (file);
				client.Commit ("1", null, false, false, null, null, null, null, "user");
				File.WriteAllText (file, "2\n");
				diffText = client.Diff (null, file);
			}
			
			string[] lines = diffText.Split (new[]{"\n"}, StringSplitOptions.RemoveEmptyEntries);
			Assert.Equal (6, lines.Length);
			Assert.Equal ("@@ -1,1 +1,1 @@", lines [3]);
			Assert.Equal ("-1", lines [4]);
			Assert.Equal ("+2", lines [5]);
		}
		
		[Fact]
		public void TestExport ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			string diffText = string.Empty;
			CommandClient.Initialize (path, MercurialPath);
			
			using (var client = new CommandClient (path, null, null, MercurialPath)) {
				File.WriteAllText (file, "1\n");
				client.Add (file);
				client.Commit ("1");
				File.WriteAllText (file, "2\n");
				client.Commit ("2");
				diffText = client.Export ("1");
			}
			string[] lines = diffText.Split (new[]{"\n"}, StringSplitOptions.RemoveEmptyEntries);
			Assert.Equal (13, lines.Length);
			Assert.Equal ("@@ -1,1 +1,1 @@", lines [10]);
			Assert.Equal ("-1", lines [11]);
			Assert.Equal ("+2", lines [12]);
		}
		
		[Fact]
		public void TestForget ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			IDictionary<string,Status > statuses = null;
			
			CommandClient.Initialize (path, MercurialPath);
			using (var client = new CommandClient (path, null, null, MercurialPath)) {
				File.WriteAllText (file, string.Empty);
				client.Add (file);
				statuses = client.Status (null);
				
				Assert.NotNull (statuses);
				Assert.True (statuses.ContainsKey ("foo"), "No status received for foo");
				Assert.Equal (FileStatus.Added, statuses ["foo"]);
				
				client.Forget (file);
				statuses = client.Status ();
			}
			Assert.True (statuses.ContainsKey ("foo"), "foo is no longer known");
			Assert.Equal (FileStatus.Unknown, statuses["foo"]);
		}
		
		[Fact]
		public void TestPull ()
		{
			string firstPath = GetTemporaryPath ();
			string secondPath = GetTemporaryPath ();
			string file = Path.Combine (firstPath, "foo");
			CommandClient.Initialize (firstPath, MercurialPath);
			CommandClient firstClient = null,
			              secondClient = null;
			
			try {
				// Create repo with one commit
				firstClient = new CommandClient (firstPath, null, null, MercurialPath);
				File.WriteAllText (file, "1");
				firstClient.Add (file);
				firstClient.Commit ("1");
			
				// Clone repo
				CommandClient.Clone (source: firstPath, destination: secondPath, mercurialPath: MercurialPath);
				secondClient = new CommandClient (secondPath, null, null, MercurialPath);
				Assert.Equal (1, secondClient.Log (null).Count);
				
				// Add changeset to original repo
				File.WriteAllText (file, "2");
				firstClient.Commit ("2");
				
				// Pull from clone
				Assert.True (secondClient.Pull (null), "Pull unexpectedly resulted in unresolved files");
				Assert.Equal (2, secondClient.Log (null).Count);
			} finally {
				if (null != firstClient)
					firstClient.Dispose ();
				if (null != secondClient)
					secondClient.Dispose ();
			}
		}
		
		[Fact(Skip = "Merge tool popup")]
		public void TestMerge ()
		{
			string firstPath = GetTemporaryPath ();
			string secondPath = GetTemporaryPath ();
			string file = Path.Combine (firstPath, "foo");
			CommandClient.Initialize (firstPath, MercurialPath);
			CommandClient firstClient = null,
			              secondClient = null;
			
			try {
				// Create repo with one commit
				firstClient = new CommandClient (firstPath, null, null, MercurialPath);
				File.WriteAllText (file, "1\n");
				firstClient.Add (file);
				firstClient.Commit ("1");
			
				// Clone repo
				CommandClient.Clone (source: firstPath, destination: secondPath, mercurialPath: MercurialPath);
				secondClient = new CommandClient (secondPath, null, null, MercurialPath);
				Assert.Equal (1, secondClient.Log (null).Count);
				
				// Add changeset to original repo
				File.WriteAllText (file, "2\n");
				firstClient.Commit ("2");
				
				// Add non-conflicting changeset to child repo
				File.WriteAllText (Path.Combine (secondPath, "foo"), "1\na\n");
				secondClient.Commit ("a");
				
				// Pull from clone
				Assert.True (secondClient.Pull (null), "Pull unexpectedly resulted in unresolved files");
				Assert.Equal (3, secondClient.Log (null).Count);
				
				Assert.Equal (2, secondClient.Heads ().Count());
				
				Assert.True (secondClient.Merge (null), "Merge unexpectedly resulted in unresolved files");
			} finally {
				if (null != firstClient)
					firstClient.Dispose ();
				if (null != secondClient)
					secondClient.Dispose ();
			}
		}
		
		[Fact]
		public void TestHeads ()
		{
			string firstPath = GetTemporaryPath ();
			string secondPath = GetTemporaryPath ();
			string file = Path.Combine (firstPath, "foo");
			CommandClient.Initialize (firstPath, MercurialPath);
			CommandClient firstClient = null,
			              secondClient = null;
			
			try {
				// Create repo with one commit
				firstClient = new CommandClient (firstPath, null, null, MercurialPath);
				File.WriteAllText (file, "1\n");
				firstClient.Add (file);
				firstClient.Commit ("1");
			
				// Clone repo
				CommandClient.Clone (source: firstPath, destination: secondPath, mercurialPath: MercurialPath);
				secondClient = new CommandClient (secondPath, null, null, MercurialPath);
				Assert.Equal (1, secondClient.Log (null).Count);
				
				// Add changeset to original repo
				File.WriteAllText (file, "2\n");
				firstClient.Commit ("2");
				
				// Add non-conflicting changeset to child repo
				File.WriteAllText (Path.Combine (secondPath, "foo"), "1\na\n");
				secondClient.Commit ("a");
				
				// Pull from clone
				Assert.True (secondClient.Pull (null), "Pull unexpectedly resulted in unresolved files");
				Assert.Equal (3, secondClient.Log (null).Count);
				Assert.Equal (2, secondClient.Heads ().Count());
			}
            finally
            {
				if (null != firstClient)
					firstClient.Dispose();
				if (null != secondClient)
					secondClient.Dispose();
			}
		}
		
		[Fact]
		public void TestPush ()
		{
			string firstPath = GetTemporaryPath ();
			string secondPath = GetTemporaryPath ();
			string file = Path.Combine (firstPath, "foo");
			CommandClient.Initialize (firstPath, MercurialPath);
			CommandClient firstClient = null,
			              secondClient = null;
			
			try {
				// Create repo with one commit
				firstClient = new CommandClient (firstPath, null, null, MercurialPath);
				File.WriteAllText (file, "1\n");
				firstClient.Add (file);
				firstClient.Commit ("1");
			
				// Clone repo
				CommandClient.Clone (source: firstPath, destination: secondPath, mercurialPath: MercurialPath);
				secondClient = new CommandClient (secondPath, null, null, MercurialPath);
				Assert.Equal (1, secondClient.Log (null).Count);
				
				// Add changeset to child repo
				File.WriteAllText (Path.Combine (secondPath, "foo"), "1\na\n");
				secondClient.Commit ("a");
				
				// Push to parent
				Assert.True (secondClient.Push (firstPath, null), "Nothing to push");
				
				// Assert that the first repo now has two revisions in the log
				Assert.Equal (2, firstClient.Log (null, firstPath).Count);
			} finally {
				if (null != firstClient)
					firstClient.Dispose ();
				if (null != secondClient)
					secondClient.Dispose ();
			}
		}
		
		[Fact]
		public void TestSummary ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			string summary = string.Empty;
			CommandClient.Initialize (path, MercurialPath);
			
			using (var client = new CommandClient (path, null, null, MercurialPath)) {
				File.WriteAllText (file, "1");
				client.Add (file);
				client.Commit ("1", null, false, false, null, null, null, null, "user");
				summary = client.Summary (false);
			}
			
			Assert.True (summary.Contains ("branch: default"));
		}
		
		[Fact]
		public void TestIncoming ()
		{
			string firstPath = GetTemporaryPath ();
			string secondPath = GetTemporaryPath ();
			string file = Path.Combine (firstPath, "foo");
			CommandClient.Initialize (firstPath, MercurialPath);
			CommandClient firstClient = null,
			              secondClient = null;
			
			try {
				// Create repo with one commit
				firstClient = new CommandClient (firstPath, null, null, MercurialPath);
				File.WriteAllText (file, "1");
				firstClient.Add (file);
				firstClient.Commit ("1");
			
				// Clone repo
				CommandClient.Clone (source: firstPath, destination: secondPath, mercurialPath: MercurialPath);
				secondClient = new CommandClient (secondPath, null, null, MercurialPath);
				Assert.Equal (1, secondClient.Log (null).Count);
				
				// Add changesets to original repo
				File.WriteAllText (file, "2");
				firstClient.Commit ("2");
				File.WriteAllText (file, "3");
				firstClient.Commit ("3");
				
				IList<Revision > incoming = secondClient.Incoming (null, null);
				Assert.Equal (2, incoming.Count);
			}
            finally
            {
				if (null != firstClient)
					firstClient.Dispose ();
				if (null != secondClient)
					secondClient.Dispose ();
			}
		}

		[Fact]
		public void TestOutgoing ()
		{
			string firstPath = GetTemporaryPath ();
			string secondPath = GetTemporaryPath ();
			string file = Path.Combine (firstPath, "foo");
			CommandClient.Initialize (firstPath, MercurialPath);
			CommandClient firstClient = null,
			              secondClient = null;
			
			try {
				// Create repo with one commit
				firstClient = new CommandClient (firstPath, null, null, MercurialPath);
				File.WriteAllText (file, "1");
				firstClient.Add (file);
				firstClient.Commit ("1");
			
				// Clone repo
				CommandClient.Clone (source: firstPath, destination: secondPath, mercurialPath: MercurialPath);
				secondClient = new CommandClient (secondPath, null, null, MercurialPath);
				Assert.Equal (1, secondClient.Log (null).Count);
				
				// Add changeset to original repo
				File.WriteAllText (file, "2");
				firstClient.Commit ("2");
				File.WriteAllText (file, "3");
				firstClient.Commit ("3");
				
				IList<Revision > outgoing = firstClient.Outgoing (secondPath, null);
				Assert.Equal (2, outgoing.Count);
			} finally {
				if (null != firstClient)
					firstClient.Dispose ();
				if (null != secondClient)
					secondClient.Dispose ();
			}
		}
		
		[Fact]
		public void TestVersion ()
		{
			string path = GetTemporaryPath();
			CommandClient.Initialize (path, MercurialPath);
			Regex versionRegex = new Regex (@"^\d\.\d\.\d.*$");

            using (var client = new CommandClient(path, null, null, MercurialPath))
            {
                Match match = versionRegex.Match(client.Version);
                Assert.NotNull(match);
                Assert.True(match.Success, "Invalid version string");
            }
		}
		
		[Fact]
		public void TestRevert ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			CommandClient.Initialize (path, MercurialPath);

            using (var client = new CommandClient(path, null, null, MercurialPath))
            {
                File.WriteAllText(file, string.Empty);
                client.Add(file);
                client.Commit("Commit all");
                Assert.True(!client.Status().ContainsKey("foo"), "Default commit failed for foo");

                File.WriteAllText(file, "Modified!");
                Assert.True(client.Status().ContainsKey("foo"), "Failed to modify file");
                client.Revert(null, file);
                Assert.True(!client.Status().ContainsKey("foo"), "Revert failed for foo");
            }
		}
		
		[Fact]
		public void TestRename ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			CommandClient.Initialize (path, MercurialPath);
			using (var client = new CommandClient (path, null, null, MercurialPath)) {
				File.WriteAllText (file, string.Empty);
				client.Add (file);
				client.Commit ("Commit all");
				Assert.True (!client.Status ().ContainsKey ("foo"), "Default commit failed for foo");

				client.Rename ("foo", "foo2");
				IDictionary<string,Status > statuses = client.Status ();
				statuses = client.Status (new[]{path}, quiet: false);
				Assert.Equal (FileStatus.Removed, statuses ["foo"]);
				Assert.Equal (FileStatus.Added, statuses ["foo2"]);
				
				client.Commit ("Commit rename");
				Assert.True (!client.Status ().ContainsKey ("foo"), "Failed to rename file");
				Assert.True (!client.Status ().ContainsKey ("foo2"), "Failed to rename file");
				Assert.True (!File.Exists (file));
				Assert.True (File.Exists (Path.Combine (path, "foo2")));
			}
		}

		[Fact]
		public void TestCat ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			CommandClient.Initialize (path, MercurialPath);
			using (var client = new CommandClient (path, null, null, MercurialPath)) {
				File.WriteAllText (file, "foo\n");
				client.Add (Path.Combine (path, "foo"));
				client.Commit ("Commit all");
				Assert.True (!client.Status ().ContainsKey ("foo"), "Default commit failed for foo");
				
				var contents = client.Cat (null, file);
				Assert.Equal (1, contents.Count);
				Assert.True (contents.ContainsKey (file), "foo not in file set");
				Assert.Equal ("foo\n", contents [file]);
			}
		}
		
		[Fact]
		public void TestCatMore ()
		{
			string path = GetTemporaryPath();
			string file = Path.Combine (path, "foo");

			CommandClient.Initialize (path, MercurialPath);
            File.WriteAllText(file, "test text");

            using (var client = new CommandClient(path, null, null, MercurialPath))
            {
                client.Add(file);
                client.Commit("Commit all");
                Assert.True(!client.Status().ContainsKey("foo"), "Default commit failed for foo");

                var contents = client.Cat(null, file);
                Assert.Equal(1, contents.Count);
                Assert.True(contents.ContainsKey(file), "foo not in file set");
                Assert.Equal(File.ReadAllText(file), contents[file]);
            }
		}
		
		[Fact]
		public void TestRemove ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			CommandClient.Initialize (path, MercurialPath);

            using (var client = new CommandClient(path, null, null, MercurialPath))
            {
                File.WriteAllText(file, string.Empty);
                client.Add(file);
                client.Commit("Commit all");
                Assert.True(!client.Status().ContainsKey(file), "Default commit failed for foo");

                client.Remove(file);
                Assert.True(!File.Exists(file));

                IDictionary<string, Status> statuses = client.Status();
                Assert.True(statuses.ContainsKey("foo"), "No status for foo");
                Assert.Equal(FileStatus.Removed, statuses["foo"]);
            }
		}
		
		[Fact(Skip = "Merge client popup")]
		public void TestResolve ()
		{
			string firstPath = GetTemporaryPath ();
			string secondPath = GetTemporaryPath ();
			string file = Path.Combine (firstPath, "foo");
			CommandClient.Initialize (firstPath, MercurialPath);
			CommandClient firstClient = null,
			              secondClient = null;
			
			try {
				// Create repo with one commit
				firstClient = new CommandClient (firstPath, null, null, MercurialPath);
				File.WriteAllText (file, "1\n");
				firstClient.Add (file);
				firstClient.Commit ("1");
			
				// Clone repo
				CommandClient.Clone (source: firstPath, destination: secondPath, mercurialPath: MercurialPath);
				secondClient = new CommandClient (secondPath, null, null, MercurialPath);
				Assert.Equal (1, secondClient.Log (null).Count);
				
				// Add changeset to original repo
				File.WriteAllText (file, "2\n");
				firstClient.Commit ("2");
				
				// Add non-conflicting changeset to child repo
				File.WriteAllText (Path.Combine (secondPath, "foo"), "1\na\n");
				secondClient.Commit ("a");
				
				// Pull from clone
				Assert.True (secondClient.Pull (null), "Pull unexpectedly resulted in unresolved files");
				Assert.Equal (3, secondClient.Log (null).Count);
				
				Assert.Equal (2, secondClient.Heads ().Count());
				
				Assert.True (secondClient.Merge (null), "Merge unexpectedly resulted in unresolved files");
				
				IDictionary<string,bool > statuses = secondClient.Resolve (null, true, true, false, false, null, null, null);
				Assert.True (statuses.ContainsKey ("foo"), "No merge status for foo");
				Assert.Equal (true, statuses ["foo"]);
			}
            finally
            {
				if (null != firstClient)
					firstClient.Dispose ();
				if (null != secondClient)
					secondClient.Dispose ();
			}
		}
		
		[Fact]
		public void TestParents ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			CommandClient.Initialize (path, MercurialPath);

            using (var client = new CommandClient(path, null, null, MercurialPath))
            {
                File.WriteAllText(file, string.Empty);
                client.Add(file);
                client.Commit("Commit all");
                Assert.True(!client.Status().ContainsKey(file), "Default commit failed for foo");

                IEnumerable<Revision> parents = client.Parents(file, null);
                Assert.Equal(1, parents.Count());
            }
		}
		
		[Fact]
		public void TestStatus ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			string unknownFile = Path.Combine (path, "bar");
			CommandClient.Initialize (path, MercurialPath);

            using (var client = new CommandClient(path, null, null, MercurialPath))
            {
                File.WriteAllText(file, string.Empty);
                File.WriteAllText(unknownFile, string.Empty);
                client.Add(file);
                IDictionary<string, Status> statuses = client.Status(path);
                Assert.True(statuses.ContainsKey("foo"), "foo not found in status");
                Assert.True(statuses.ContainsKey("bar"), "bar not found in status");
                Assert.Equal(FileStatus.Added, statuses["foo"]);
                Assert.Equal(statuses["bar"], FileStatus.Unknown);

                statuses = client.Status(new[] { path }, quiet: true);
                Assert.True(statuses.ContainsKey("foo"), "foo not found in status");
                Assert.Equal(FileStatus.Added, statuses["foo"]);
                Assert.True(!statuses.ContainsKey("bar"), "bar listed in quiet status output");

                statuses = client.Status(new[] { path }, onlyFilesWithThisStatus: FileStatus.Added);
                Assert.True(statuses.ContainsKey("foo"), "foo not found in status");
                Assert.Equal(FileStatus.Added, statuses["foo"]);
                Assert.True(!statuses.ContainsKey("bar"), "bar listed in added-only status output");
            }
		}
		
		[Fact]
		public void TestRollback ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			CommandClient.Initialize (path, MercurialPath);
			using (var client = new CommandClient (path, null, null, MercurialPath)) {
				File.WriteAllText (file, string.Empty);
				client.Add (file);
				client.Commit (file);
				File.WriteAllText (file, file);
				client.Commit (file);
				Assert.Equal (2, client.Log (null).Count);
				Assert.True (client.Rollback ());
				Assert.Equal (1, client.Log (null).Count);
				Assert.Equal (FileFileStatus.Modified, client.Status (file) ["foo"]);
			}
		}
		
		[Fact]
		public void VerifyArchiveTypeCoverage ()
		{
			foreach (ArchiveType type in Enum.GetValues (typeof (ArchiveType))) {
				Console.WriteLine (Mercurial.CommandClient.archiveTypeToArgumentStringMap [type]);
			}
		}
		
		[Fact]
		public void TestArchive ()
		{
			string path = GetTemporaryPath ();
			string archivePath = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			CommandClient.Initialize (path, MercurialPath);
			using (var client = new CommandClient (path, null, null, MercurialPath)) {
				File.WriteAllText (file, string.Empty);
				client.Add (file);
				client.Commit (file);
				client.Archive (archivePath);
				Assert.True (Directory.Exists (archivePath));
				Assert.True (!Directory.Exists (Path.Combine (archivePath, ".hg")));
				Assert.True (File.Exists (Path.Combine (archivePath, "foo")));
			}
		}

		static string GetTemporaryPath ()
		{
			string dirName = string.Format("Mercuria.Tests.{0}", Path.GetRandomFileName());
			string path = Path.Combine (Path.GetTempPath (), dirName);
			if (IsRunningOnMac ()) {
				// HACK: tmp path is weird on osx
				path = "/private" + path;
			}
			garbage.Add (path);
			return path;
		}
	}
}

