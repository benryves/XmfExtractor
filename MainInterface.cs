using System;
using System.IO;
using System.Windows.Forms;


namespace XmfExtractor {
	public partial class MainInterface : Form {

		public MainInterface() {
			InitializeComponent();
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e) {
			if (this.openXmfFileDialog.ShowDialog(this) == DialogResult.OK) {
				this.OpenFile(openXmfFileDialog.FileName);
			}
		}

		private void OpenFile(string filename) {
			try {
				using (var stream = File.OpenRead(filename)) {
					var xmf = Xmf.FromStream(stream);
					this.listView.Items.Clear();
					ExtractFiles(stream, xmf.RootNode);
				}
			} catch (Exception ex) {
				MessageBox.Show(this, "Error loading file: " + ex.Message, "Open", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void ExtractFiles(Stream stream, Node node) {
			if (node.Children != null) {
				foreach (var child in node.Children) {
					ExtractFiles(stream, child);
				}
			} else {
				string filename = null;
				foreach (var meta in node.MetaData) {
					switch (meta.FieldSpecifier) {
						case FieldSpecifier.FilenameOnDisk:
							filename = meta.GetStringValue();
							break;
					}
				}
				if (!string.IsNullOrEmpty(filename)) {
					listView.Items.Add(new ListViewItem {
						Text = filename,
						Tag = node.GetFileData(stream),
						Selected = true,
					});
				}
			}
		}

		private void saveToolStripMenuItem_Click(object sender, EventArgs e) {
			foreach (ListViewItem lvi in listView.SelectedItems) {
				saveFileDialog.FileName = Path.GetFileName(lvi.Text);
				if (saveFileDialog.ShowDialog(this) == DialogResult.OK) {
					try {
						File.WriteAllBytes(saveFileDialog.FileName, (byte[])lvi.Tag);
					} catch (Exception ex) {
						MessageBox.Show(this, "Error saving file '" + lvi.Text + "': " + ex.Message, "Open", MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
				}
			}
		}
	}
}
