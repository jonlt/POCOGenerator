using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POCOGenerator
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {

            var connectionString = GenerateConnectionString();

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }

                    var databases = connection.GetSchema("Databases");
                    foreach (DataRow database in databases.Rows)
                    {
                        var databaseName = database.Field<String>("database_name");
                        cbDatabases.Items.Add(databaseName);
                    }
                    cbDatabases.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void cbDatabases_SelectedIndexChanged(object sender, EventArgs e)
        {
            cblTables.Items.Clear();
            var connectionString = GenerateConnectionString();
            using (var connection = new SqlConnection(connectionString))
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                var schema = connection.GetSchema("Tables");
                var TableNames = new List<string>();
                foreach (DataRow row in schema.Rows.OfType<DataRow>().OrderBy(r => r[2].ToString()))
                {
                    var tableName = row[2].ToString();
                    cblTables.Items.Add(tableName);
                }

            }
        }

        private string GenerateConnectionString()
        {
            var builder = new SqlConnectionStringBuilder();
            builder.DataSource = tbServer.Text;
            if (!string.IsNullOrEmpty(tbUser.Text) && !string.IsNullOrEmpty(tbPassword.Text))
            {
                builder.UserID = tbUser.Text;
                builder.Password = tbPassword.Text;
            }
            else
            {
                builder.IntegratedSecurity = true;
            }

            if (!string.IsNullOrEmpty(cbDatabases.Text))
            {
                builder.InitialCatalog = cbDatabases.Text;
            }

            return builder.ToString();
        }

        private void btnGenerate_Click(object sender, EventArgs e)
        {
            var folder = "";
            if (folderBrowserDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                folder = folderBrowserDialog1.SelectedPath;
            }
            else
            {
                return;
            }
            
            int classCount = 0;
            var fileContentBuilder = new StringBuilder();

            var fileTemplate = GetDefaultFileTemplate();
            if (File.Exists("FileTemplate.txt"))
            {
                fileTemplate = File.ReadAllText("FileTemplate.txt");
            }

            fileTemplate = fileTemplate.Replace("{{NAMESPACE}}", tbNamespace.Text);

            var connectionString = GenerateConnectionString();
            using (var connection = new SqlConnection(connectionString))
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                foreach (var item in cblTables.CheckedItems.OfType<string>())
                {
                    classCount++;
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = string.Format("SELECT * FROM {0} WHERE 0=1", item);
                        using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                        {
                            DataSet dataSet = new DataSet();
                            adapter.Fill(dataSet);
                            adapter.FillSchema(dataSet, SchemaType.Source);
                            var table = dataSet.Tables[0];
                            var classString = GeneratePOCOClass(folder, item, table);
                            if (!cbSingleFile.Checked)
	                        {
                                File.WriteAllText(Path.Combine(folder, item + ".cs"), fileTemplate.Replace("{{CLASSES}}", classString));
	                        }
                            else
                            {
                                fileContentBuilder.Append(classString);
                            }
                            
                        }
                    }
                }
            }

            if (cbSingleFile.Checked)
	        {
                File.WriteAllText(Path.Combine(folder, tbNamespace.Text + ".cs"), fileTemplate.Replace("{{CLASSES}}", fileContentBuilder.ToString()));
	        }

            MessageBox.Show(string.Format("{0} classes generated", classCount));
        }

        private string GeneratePOCOClass(string folder, string name, DataTable table)
        {
            var propertyList = new List<string>();
            foreach (DataColumn column in table.Columns)
            {
                propertyList.Add(string.Format("public {0} {1} {{ get; set; }}", GetCLRTypeName(column.DataType, column.AllowDBNull), column.ColumnName));
            }

            var classTemplate = GetDefaultClassTemplate();
            if (File.Exists("ClassTemplate.txt"))
            {
                classTemplate = File.ReadAllText("ClassTemplate.txt");
            }
                
            var properties = string.Join("\r\n", propertyList);
            classTemplate = classTemplate.Replace("{{CLASSNAME}}", tbClassPrefix.Text + name);
            classTemplate = classTemplate.Replace("{{PROPERTIES}}", properties);

            return classTemplate;
        }

        private string GetCLRTypeName(Type type, bool nullable)
        {
            var typeName = type.FullName;
            if (nullable && type.IsValueType)
            {
                typeName += "?";
            }
            return typeName;
        }

        private void cbShowPassword_CheckedChanged(object sender, EventArgs e)
        {
            tbPassword.PasswordChar = cbShowPassword.Checked ? '\0' : '*';
        }

        private void cbAllTables_CheckedChanged(object sender, EventArgs e)
        {
            for (int i = 0; i < cblTables.Items.Count; i++)
            {
                cblTables.SetItemChecked(i, cbAllTables.Checked);
            }
        }

        private string GetDefaultFileTemplate()
        {
            string template = "// This file is auto-generated\r\nusing System;\r\nusing System.Text;\r\n\r\nnamespace {{NAMESPACE}}\r\n{\r\n{{CLASSES}}\r\n}";
            return template;
        }

        private string GetDefaultClassTemplate()
        {
            string template = "public class {{CLASSNAME}}\r\n{\r\n{{PROPERTIES}}\r\n}\r\n";
            return template;
        }
    }
}
