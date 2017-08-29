# Web38
Багатопоточний посередник між 1С 8.3 та сайтом наприклад

Принцип роботи
Відкриває порт наприклад 5656 для прийому вхідних підключень по протоколу TCP
Одночасно запускає декілька робочих потоків, які підключаються до 1С 8.3 через COM

Клієнт пересилає запит у форматі ХМЛ. В запиті вказується яку функцію потрібно викликати в модулі
зовнішнього з'єднання та з якими параметрами. Результат функції також у форматі ХМЛ пересилаються 
клієнту.

Виклик функції в 1С

<script runat="server">
    protected void LogOn_Click(object sender, EventArgs e)
    {
        ProtocolWeb38 Web38 = new ProtocolWeb38("LogIn",
            new string[]
            {
                UserLogin.Text,
                UserPassword.Text,
                Request.UserHostAddress
            });
            
        Web38.Send();
        
        if (Web38.ReceivePacket.StateCode == "200")
        {
            //Код 200 значить що все ОК
            Session["USER_GUID"] = Web38.ReceivePacket.Guid;
            Response.Redirect("update_acaunt.aspx");
        }
        else
        {
            Label labmsg = new Label();
            labmsg.Text = Web38.ReceivePacket.Info;
            PlaceHolderMsg.Controls.Add(labmsg);
        }
    }
</script>
