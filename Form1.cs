namespace FileCompare
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            // wire up copy buttons
            btnCopyFromRight.Click += BtnCopyFromRight_Click;
            btnCopyFromLeft.Click += BtnCopyFromLeft_Click;
            // helper to inspect items that only show a name
            lvwLeftDir.DoubleClick += Lvw_DoubleClick;
            lvwRightDir.DoubleClick += Lvw_DoubleClick;
        }

        private long GetDirectorySizeSafe(string path)
        {
            try
            {
                return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                    .Where(f => {
                        try { var attr = File.GetAttributes(f); return !attr.HasFlag(FileAttributes.Hidden) && !attr.HasFlag(FileAttributes.System); }
                        catch { return false; }
                    })
                    .Select(f => {
                        try { return new FileInfo(f).Length; } catch { return 0L; }
                    }).Sum();
            }
            catch
            {
                return 0L;
            }
        }

        private class FileEntry
        {
            public string Name { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public bool IsDirectory { get; set; }
            public long Length { get; set; }
            public DateTime LastWriteTimeUtc { get; set; }
            public DateTime LastWriteTimeLocal { get; set; }
        }

        private void Lvw_DoubleClick(object? sender, EventArgs e)
        {
            if (sender is ListView lv && lv.SelectedItems.Count > 0)
            {
                var it = lv.SelectedItems[0];
                var path = it.Tag as string;
                if (string.IsNullOrWhiteSpace(path))
                {
                    MessageBox.Show(this, $"항목: {it.Text}\n해당 항목에 연결된 파일 경로가 없습니다.", "항목 정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(this, $"항목: {it.Text}\n경로: {path}", "항목 정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }




        private void btnLeftDir_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "폴더를 선택하세요.";
                // 현재 텍스트박스에 있는 경로를 초기 선택 폴더로 설정
                if (!string.IsNullOrWhiteSpace(txtLeftDir.Text) &&
                        Directory.Exists(txtLeftDir.Text))
                {
                    dlg.SelectedPath = txtLeftDir.Text;
                }
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    txtLeftDir.Text = dlg.SelectedPath;
                    UpdateFileLists();
                }
            }
        }
        private void PopulateListView(ListView lv, string folderPath)
        {
            lv.BeginUpdate();
            lv.Items.Clear();
            try
            {
                // 폴더(디렉터리) 먼저 추가
                var dirs = Directory.EnumerateDirectories(folderPath)
                           .Select(p => new DirectoryInfo(p)).OrderBy(d => d.Name);
                foreach (var d in dirs)
                {
                    var item = new ListViewItem(d.Name);
                    // show actual directory size (sum of files)
                    var dirSize = GetDirectorySizeSafe(d.FullName);
                    item.SubItems.Add(dirSize.ToString("N0") + " 바이트");
                    item.SubItems.Add(d.LastWriteTime.ToString("g"));
                    lv.Items.Add(item);
                }

                // 파일 추가
                var files = Directory.EnumerateFiles(folderPath)
                            .Select(p => new FileInfo(p)).OrderBy(f => f.Name);
                foreach (var f in files)
                {
                    var item = new ListViewItem(f.Name);
                    item.SubItems.Add(f.Length.ToString("N0") + " 바이트");
                    item.SubItems.Add(f.LastWriteTime.ToString("g"));
                    lv.Items.Add(item);
                }

                // 컬럼 너비 자동 조정(컨텐츠 기준)
                for (int i = 0; i < lv.Columns.Count; i++)
                {
                    lv.AutoResizeColumn(i, ColumnHeaderAutoResizeStyle.ColumnContent);
                }
            }
            catch (DirectoryNotFoundException)
            {
                MessageBox.Show(this, "폴더를 찾을 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(this, "폴더에 접근할 수 없습니다: " + folderPath, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (IOException ex)
            {
                MessageBox.Show(this, "입출력 오류: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                lv.EndUpdate();
            }
        }




        private void btnRightDir_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "폴더를 선택하세요.";
                // 현재 텍스트박스에 있는 경로를 초기 선택 폴더로 설정
                if (!string.IsNullOrWhiteSpace(txtRightDir.Text) &&
                        Directory.Exists(txtRightDir.Text))
                {
                    dlg.SelectedPath = txtRightDir.Text;
                }
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    txtRightDir.Text = dlg.SelectedPath;
                    UpdateFileLists();
                }
            }
        }

        private void UpdateFileLists()
        {
            var leftPath = txtLeftDir.Text;
            var rightPath = txtRightDir.Text;

            var leftFiles = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
            var rightFiles = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);

            var leftEntries = new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
            var rightEntries = new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(leftPath) && Directory.Exists(leftPath))
            {
                // directories
                foreach (var d in Directory.EnumerateDirectories(leftPath).Select(p => new DirectoryInfo(p)).OrderBy(d => d.Name))
                {
                    if (d.Attributes.HasFlag(FileAttributes.Hidden) || d.Attributes.HasFlag(FileAttributes.System))
                        continue;
                    leftEntries[d.Name] = new FileEntry
                    {
                        Name = d.Name,
                        FullName = d.FullName,
                        IsDirectory = true,
                        Length = 0,
                        LastWriteTimeUtc = d.LastWriteTimeUtc,
                        LastWriteTimeLocal = d.LastWriteTime
                    };
                }

                // files
                foreach (var f in Directory.EnumerateFiles(leftPath).Select(p => new FileInfo(p)).OrderBy(f => f.Name))
                {
                    // skip hidden/system and common system temp files
                    if (f.Attributes.HasFlag(FileAttributes.Hidden) || f.Attributes.HasFlag(FileAttributes.System))
                        continue;
                    var lname = f.Name;
                    if (string.Equals(lname, "desktop.ini", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(lname, "Thumbs.db", StringComparison.OrdinalIgnoreCase) ||
                        lname.StartsWith("~$"))
                        continue;

                    leftEntries[f.Name] = new FileEntry
                    {
                        Name = f.Name,
                        FullName = f.FullName,
                        IsDirectory = false,
                        Length = f.Length,
                        LastWriteTimeUtc = f.LastWriteTimeUtc,
                        LastWriteTimeLocal = f.LastWriteTime
                    };
                }
            }
            if (!string.IsNullOrWhiteSpace(rightPath) && Directory.Exists(rightPath))
            {
                // directories
                foreach (var d in Directory.EnumerateDirectories(rightPath).Select(p => new DirectoryInfo(p)).OrderBy(d => d.Name))
                {
                    if (d.Attributes.HasFlag(FileAttributes.Hidden) || d.Attributes.HasFlag(FileAttributes.System))
                        continue;
                    rightEntries[d.Name] = new FileEntry
                    {
                        Name = d.Name,
                        FullName = d.FullName,
                        IsDirectory = true,
                        Length = 0,
                        LastWriteTimeUtc = d.LastWriteTimeUtc,
                        LastWriteTimeLocal = d.LastWriteTime
                    };
                }

                // files
                foreach (var f in Directory.EnumerateFiles(rightPath).Select(p => new FileInfo(p)).OrderBy(f => f.Name))
                {
                    // skip hidden/system and common system temp files
                    if (f.Attributes.HasFlag(FileAttributes.Hidden) || f.Attributes.HasFlag(FileAttributes.System))
                        continue;
                    var rname = f.Name;
                    if (string.Equals(rname, "desktop.ini", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(rname, "Thumbs.db", StringComparison.OrdinalIgnoreCase) ||
                        rname.StartsWith("~$"))
                        continue;

                    rightEntries[f.Name] = new FileEntry
                    {
                        Name = f.Name,
                        FullName = f.FullName,
                        IsDirectory = false,
                        Length = f.Length,
                        LastWriteTimeUtc = f.LastWriteTimeUtc,
                        LastWriteTimeLocal = f.LastWriteTime
                    };
                }
            }

            // decide name set based on which folders are present
            var allNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var leftExists = !string.IsNullOrWhiteSpace(leftPath) && Directory.Exists(leftPath) && leftEntries.Count > 0;
            var rightExists = !string.IsNullOrWhiteSpace(rightPath) && Directory.Exists(rightPath) && rightEntries.Count > 0;

            if (leftExists && rightExists)
            {
                // When both folders are selected, show only files that actually exist on each side.
                // Left list: iterate leftFiles; Right list: iterate rightFiles.

                // populate left list
                lvwLeftDir.BeginUpdate();
                try
                {
                    lvwLeftDir.Items.Clear();
                    foreach (var lf in leftEntries.Values.OrderBy(f => f.Name))
                    {
                        leftEntries.TryGetValue(lf.Name, out var leftEntry);
                        rightEntries.TryGetValue(lf.Name, out var rf);
                        var item = new ListViewItem(lf.Name);
                        if (lf.IsDirectory)
                            item.SubItems.Add("<DIR>");
                        else
                            item.SubItems.Add(lf.Length.ToString("N0") + " 바이트");
                        item.SubItems.Add(lf.LastWriteTimeLocal.ToString("g"));
                        item.Tag = lf.FullName;

                        if (rf != null)
                        {
                            var lUtc = lf.LastWriteTimeUtc;
                            var rUtc = rf.LastWriteTimeUtc;
                            if (lUtc == rUtc)
                                item.ForeColor = Color.Black; // same
                            else if (lUtc > rUtc)
                                item.ForeColor = Color.Red; // left is newer
                            else
                                item.ForeColor = Color.Black; // left older -> black
                        }
                        else
                        {
                            item.ForeColor = Color.Purple; // only on left
                        }

                        lvwLeftDir.Items.Add(item);
                    }
                }
                finally
                {
                    lvwLeftDir.EndUpdate();
                }

                // populate right list
                lvwRightDir.BeginUpdate();
                try
                {
                    lvwRightDir.Items.Clear();
                    foreach (var rf in rightEntries.Values.OrderBy(f => f.Name))
                    {
                        leftEntries.TryGetValue(rf.Name, out var lf);
                        var item = new ListViewItem(rf.Name);
                        if (rf.IsDirectory)
                            item.SubItems.Add("<DIR>");
                        else
                            item.SubItems.Add(rf.Length.ToString("N0") + " 바이트");
                        item.SubItems.Add(rf.LastWriteTimeLocal.ToString("g"));
                        item.Tag = rf.FullName;

                        if (lf != null)
                        {
                            var lUtc = lf.LastWriteTimeUtc;
                            var rUtc = rf.LastWriteTimeUtc;
                            if (rUtc == lUtc)
                                item.ForeColor = Color.Black; // same
                            else if (rUtc > lUtc)
                                item.ForeColor = Color.Red; // right is newer
                            else
                                item.ForeColor = Color.Black; // right older -> black
                        }
                        else
                        {
                            item.ForeColor = Color.Purple; // only on right
                        }

                        lvwRightDir.Items.Add(item);
                    }
                }
                finally
                {
                    lvwRightDir.EndUpdate();
                }
            }
            else if (leftExists)
            {
                // only left folder selected: show only left list
                lvwLeftDir.BeginUpdate();
                try
                {
                    lvwLeftDir.Items.Clear();
                    foreach (var lf in leftFiles.Values.OrderBy(f => f.Name))
                    {
                        var item = new ListViewItem(lf.Name);
                        item.SubItems.Add(lf.Length.ToString("N0") + " 바이트");
                        item.SubItems.Add(lf.LastWriteTime.ToString("g"));
                        item.Tag = lf.FullName;
                        item.ForeColor = Color.Black;
                        lvwLeftDir.Items.Add(item);
                    }
                }
                finally { lvwLeftDir.EndUpdate(); }

                // clear right
                lvwRightDir.BeginUpdate();
                try { lvwRightDir.Items.Clear(); }
                finally { lvwRightDir.EndUpdate(); }
            }
            else if (rightExists)
            {
                // only right folder selected: show only right list
                lvwRightDir.BeginUpdate();
                try
                {
                    lvwRightDir.Items.Clear();
                    foreach (var rf in rightFiles.Values.OrderBy(f => f.Name))
                    {
                        var item = new ListViewItem(rf.Name);
                        item.SubItems.Add(rf.Length.ToString("N0") + " 바이트");
                        item.SubItems.Add(rf.LastWriteTime.ToString("g"));
                        item.Tag = rf.FullName;
                        item.ForeColor = Color.Black;
                        lvwRightDir.Items.Add(item);
                    }
                }
                finally { lvwRightDir.EndUpdate(); }

                // clear left
                lvwLeftDir.BeginUpdate();
                try { lvwLeftDir.Items.Clear(); }
                finally { lvwLeftDir.EndUpdate(); }
            }
            else
            {
                // neither exists: clear both
                lvwLeftDir.BeginUpdate();
                try { lvwLeftDir.Items.Clear(); }
                finally { lvwLeftDir.EndUpdate(); }
                lvwRightDir.BeginUpdate();
                try { lvwRightDir.Items.Clear(); }
                finally { lvwRightDir.EndUpdate(); }
            }

            // adjust columns to avoid horizontal scrollbar
            AdjustListViewColumns(lvwLeftDir);
            AdjustListViewColumns(lvwRightDir);
        }

        private void AdjustListViewColumns(ListView lv)
        {
            if (lv == null || lv.Columns.Count < 3) return;

            // Auto size the size and date columns to header/content
            try
            {
                lv.AutoResizeColumn(1, ColumnHeaderAutoResizeStyle.HeaderSize);
                lv.AutoResizeColumn(2, ColumnHeaderAutoResizeStyle.HeaderSize);

                int sizeCol = lv.Columns[1].Width;
                int dateCol = lv.Columns[2].Width;

                // subtract a margin for the vertical scrollbar if present
                int scrollBarWidth = SystemInformation.VerticalScrollBarWidth;
                int remaining = lv.ClientSize.Width - sizeCol - dateCol - scrollBarWidth - 8;
                if (remaining < 50) remaining = 50;
                lv.Columns[0].Width = remaining;
            }
            catch
            {
                // ignore layout errors
            }
        }

        private void BtnCopyFromRight_Click(object? sender, EventArgs e)
        {
            // notify and Copy selected items from LEFT list to RIGHT folder
            // Copy selected items from LEFT list to RIGHT folder
            if (string.IsNullOrWhiteSpace(txtRightDir.Text) || !Directory.Exists(txtRightDir.Text))
            {
                MessageBox.Show(this, "오른쪽 폴더가 설정되어 있지 않습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var selected = lvwLeftDir.SelectedItems.Cast<ListViewItem>().ToList();
            foreach (var item in selected)
            {
                var srcPath = item.Tag as string;
                if (string.IsNullOrWhiteSpace(srcPath)) continue;
                var name = Path.GetFileName(srcPath);
                var destPath = Path.Combine(txtRightDir.Text, name);
                if (Directory.Exists(srcPath))
                {
                    CopyDirectoryWithConfirmation(srcPath, destPath);
                }
                else if (File.Exists(srcPath))
                {
                    CopyFileWithConfirmation(srcPath, destPath);
                }
            }
            UpdateFileLists();
        }

        private void BtnCopyFromLeft_Click(object? sender, EventArgs e)
        {
            // notify and Copy selected items from RIGHT list to LEFT folder
            // Copy selected items from RIGHT list to LEFT folder
            if (string.IsNullOrWhiteSpace(txtLeftDir.Text) || !Directory.Exists(txtLeftDir.Text))
            {
                MessageBox.Show(this, "왼쪽 폴더가 설정되어 있지 않습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var selected = lvwRightDir.SelectedItems.Cast<ListViewItem>().ToList();
            foreach (var item in selected)
            {
                var srcPath = item.Tag as string;
                if (string.IsNullOrWhiteSpace(srcPath)) continue;
                var name = Path.GetFileName(srcPath);
                var destPath = Path.Combine(txtLeftDir.Text, name);
                if (Directory.Exists(srcPath))
                {
                    CopyDirectoryWithConfirmation(srcPath, destPath);
                }
                else if (File.Exists(srcPath))
                {
                    CopyFileWithConfirmation(srcPath, destPath);
                }
            }
            UpdateFileLists();
        }

        private void CopyDirectoryWithConfirmation(string srcDir, string destDir)
        {
            try
            {
                var srcInfo = new DirectoryInfo(srcDir);
                var destInfo = new DirectoryInfo(destDir);

                // If destination exists and is newer than source, ask once for this directory
                if (destInfo.Exists && destInfo.LastWriteTime > srcInfo.LastWriteTime)
                {
                    var res = MessageBox.Show(this, "덮어쓰기 대상 파일이 더 최근 파일입니다. 진행하시겠습니까?",
                        "덮어쓰기 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (res != DialogResult.Yes) return;
                }

                // ensure destination directory exists
                Directory.CreateDirectory(destDir);

                // copy files
                foreach (var file in Directory.EnumerateFiles(srcDir))
                {
                    var destFile = Path.Combine(destDir, Path.GetFileName(file));
                    try
                    {
                        if (File.Exists(destFile))
                        {
                            var srcF = new FileInfo(file);
                            var destF = new FileInfo(destFile);
                            if (destF.LastWriteTime > srcF.LastWriteTime)
                            {
                                var r = MessageBox.Show(this, "덮어쓰기 대상 파일이 더 최근 파일입니다. 진행하시겠습니까?",
                                    "덮어쓰기 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                if (r != DialogResult.Yes) continue;
                            }
                        }
                        File.Copy(file, destFile, true);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "파일 복사 중 오류: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                // recurse subdirectories
                foreach (var dir in Directory.EnumerateDirectories(srcDir))
                {
                    var name = Path.GetFileName(dir);
                    var targetSub = Path.Combine(destDir, name);
                    CopyDirectoryWithConfirmation(dir, targetSub);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "폴더 복사 중 오류: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CopyFileWithConfirmation(string srcPath, string destPath)
        {
            try
            {
                if (File.Exists(destPath))
                {
                    var srcInfo = new FileInfo(srcPath);
                    var destInfo = new FileInfo(destPath);
                    // Only ask when destination is newer than source (overwrite would replace a newer file)
                    if (destInfo.LastWriteTime > srcInfo.LastWriteTime)
                    {
                        var res = MessageBox.Show(this, "덮어쓰기 대상 파일이 더 최근 파일입니다. 진행하시겠습니까?",
                            "덮어쓰기 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (res != DialogResult.Yes) return;
                    }
                }

                File.Copy(srcPath, destPath, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "파일 복사 중 오류: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void lvwLeftDir_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}
