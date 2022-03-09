using System;
using System.IO;
using System.Windows.Forms;


namespace XmfExtractor {
	public partial class MainInterface : Form {

		public MainInterface() {
			InitializeComponent();
		}

		private Stream openedXmf = null;

		private void openToolStripMenuItem_Click(object sender, EventArgs e) {
			if (this.openXmfFileDialog.ShowDialog(this) == DialogResult.OK) {
				this.OpenFile(openXmfFileDialog.FileName);
			}
		}

		private void OpenFile(string filename) {
			try {
				if (this.openedXmf != null) {
					this.openedXmf.Dispose();
					this.openedXmf = null;
				}
				this.openedXmf = File.OpenRead(filename);
				var xmf = Xmf.FromStream(this.openedXmf);
				this.listView.Items.Clear();
				ExtractFiles(this.openedXmf, xmf.RootNode);
			} catch (Exception ex) {
				if (this.openedXmf != null) {
					this.openedXmf.Dispose();
					this.openedXmf = null;
				}
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
						Tag = node,
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
						Node n = (Node)lvi.Tag;
						File.WriteAllBytes(saveFileDialog.FileName, n.GetFileData(this.openedXmf));
					} catch (Exception ex) {
						MessageBox.Show(this, "Error saving file '" + lvi.Text + "': " + ex.Message, "Open", MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
				}
			}
		}

		private void MainInterface_FormClosed(object sender, FormClosedEventArgs e) {
			if (this.openedXmf != null) {
				this.openedXmf.Dispose();
				this.openedXmf = null;
			}
		}
	}
}
