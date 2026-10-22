using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.IO.Compression;
using System.Text;
using System.Windows;
using NAudio.Wave;
namespace TetoDesk {
    public static partial class TestProgram {
        private static void PumpAccount(Task task) {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            while(!task.IsCompleted){if(watch.ElapsedMilliseconds>100000)throw new TimeoutException("Account metadata test timeout");Pump(15);}
            task.GetAwaiter().GetResult();
        }
        private static void AccountQueryChecks(bool live) {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var context=SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(app.Dispatcher));
            string fixture=Path.Combine(root,"account-query-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
            var c=new Controller(Path.Combine(fixture,"Data","session.json"),fixture,a=>a(),live?new ProviderCatalog(Path.Combine(fixture,"Data","providers.json")):null);var pet=new PetWindow(new Sprites());string tray="";
            var pending=new Queue<TaskCompletionSource<AccountQueryResult>>();int modelTurns=0;
            Func<Task<AccountQueryResult>> read=live?(Func<Task<AccountQueryResult>>)null:()=>{var t=new TaskCompletionSource<AccountQueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);pending.Enqueue(t);return t.Task;};
            var query=new AccountQuery(c,pet,label=>tray=label,read);
            var quota=QuotaSnapshot.Read(P("rateLimits",P("primary",P("usedPercent",27,"windowDurationMins",300,"resetsAt",DateTimeOffset.UtcNow.ToUnixTimeSeconds()+3600),"secondary",P("usedPercent",61,"windowDurationMins",10080,"resetsAt",DateTimeOffset.UtcNow.ToUnixTimeSeconds()+86400))));
            var balance=BalanceSnapshot.Read(P("status","ready","value",new[]{P("currency","CNY","balance","18.256")},"bonusWallets",new[]{P("currency","CNY","balance","2")}));
            Func<bool> menus=()=>tray==c.AccountQueryLabel&&pet.BuildMenu().Items.OfType<System.Windows.Controls.MenuItem>().Any(m=>Convert.ToString(m.Header)==tray)&&!pet.BuildMenu().Items.OfType<System.Windows.Controls.MenuItem>().Any(m=>Convert.ToString(m.Header)==(c.Backend=="codex"?"查询账户余额":"查询剩余额度"));
            try {
                if(live) {
                    foreach(string backend in new[]{"codex","deepseek","codex"}) {
                        Assert(c.SetBackend(backend)&&menus(),"Both production menus select the current account service: "+backend);
                        PumpAccount(c.ConnectAsync());c.Client.Event+=(sender,e)=>{if(e.Method=="turn/started")modelTurns++;};
                        c.ThreadId="fixture-"+backend;c.Messages.Add(new ChatEntry("user",Guid.NewGuid().ToString("N"),"metadata test"));c.Save();string thread=c.ThreadId;int messages=c.Messages.Count;var state=c.Status.State;
                        PumpAccount(query.RunAsync());
                        if(backend=="codex") {
                            Assert(pet.QuotaBubble.Title.Contains("剩余额度")&&pet.QuotaBubble.FiveRemainingText!="剩余 --"&&pet.QuotaBubble.WeekRemainingText!="剩余 --","The real ChatGPT route displays both 5-hour and weekly remaining percentages");
                            Assert(pet.QuotaBubble.FiveResetText!="重置时间暂不可用"&&pet.QuotaBubble.WeekResetText!="重置时间暂不可用","Both ChatGPT reset countdowns are available from the native account response");
                        } else Assert(pet.QuotaBubble.Title.Contains("DeepSeek")&&PanelVisuals((FrameworkElement)pet.QuotaBubble.Content).OfType<System.Windows.Controls.TextBlock>().Any(t=>t.Text.Contains("充值 ")&&t.Text.Contains("赠送 ")),"The real DeepSeek route displays account-wallet balance instead of ChatGPT quota");
                        Assert(c.ThreadId==thread&&c.Messages.Count==messages&&c.Status.State==state&&!c.Busy&&c.Client.Connected,"Account lookup preserves conversation and task state without an inference turn: "+backend);
                        pet.DismissQuotaInfo();
                    }
                    Assert(modelTurns==0&&!pet.IsVisible&&!pet.QuotaBubble.IsVisible,"Live provider routing queries only account metadata and shows no test window");
                    J.Save(Path.Combine(root,"account-query-live-properties.json"),P("providers",new[]{"codex","deepseek","codex"},"chatGptFiveHourAndWeek",true,"deepseekWallet",true,"modelTurns",modelTurns,"windowsShown",false,"mouseInputUsed",false,"accountRequests",3,"realAmountsLogged",false));
                } else {
                    Assert(menus()&&tray=="查询剩余额度","The ChatGPT route restores the original pet and tray quota captions");
                    var work=query.RunAsync();var request=pending.Dequeue();Pump(260);
                    Assert(pet.QuotaBubbleRevealed&&pet.QuotaBubble.Title.Contains("剩余额度")&&pet.QuotaBubble.FiveRemainingText=="查询中","ChatGPT starts the early quota-loading bubble");
                    request.SetResult(new AccountQueryResult{Backend="codex",Quota=quota});PumpAccount(work);
                    Assert(pet.QuotaBubble.FiveRemainingText=="剩余 73%"&&pet.QuotaBubble.WeekRemainingText=="剩余 39%"&&pet.QuotaBubble.FiveBarValue==73&&pet.QuotaBubble.WeekBarValue==39,"ChatGPT results restore both percentages and matching progress bars");
                    c.SetBackend("deepseek");Assert(menus()&&tray=="查询账户余额"&&!pet.QuotaVisible,"Switching to DeepSeek updates both captions and dismisses the prior quota bubble");
                    work=query.RunAsync();request=pending.Dequeue();Pump(260);Assert(pet.QuotaBubbleRevealed&&pet.QuotaBubble.Title.Contains("DeepSeek"),"DeepSeek starts the early balance-loading bubble");
                    request.SetResult(new AccountQueryResult{Backend="deepseek",Balance=balance});PumpAccount(work);
                    Assert(PanelVisuals((FrameworkElement)pet.QuotaBubble.Content).OfType<System.Windows.Controls.TextBlock>().Any(t=>t.Text=="¥20.26"),"DeepSeek renders the wallet total in the same production presenter");
                    var oldWork=query.RunAsync();var old=pending.Dequeue();c.SetBackend("codex");Assert(!pet.QuotaVisible&&menus(),"A provider change cancels an account request's visible feedback immediately");
                    work=query.RunAsync();request=pending.Dequeue();old.SetResult(new AccountQueryResult{Backend="deepseek",Balance=balance});PumpAccount(oldWork);
                    Assert(pet.QuotaVisible&&pet.QuotaBubble.Title.Contains("剩余额度")&&pet.QuotaBubble.FiveRemainingText=="查询中","A late DeepSeek balance cannot replace or close a newer ChatGPT query");
                    request.SetResult(new AccountQueryResult{Backend="codex",Quota=quota});PumpAccount(work);
                    oldWork=query.RunAsync();old=pending.Dequeue();c.SetBackend("deepseek");work=query.RunAsync();request=pending.Dequeue();old.SetException(new IOException("late ChatGPT error"));PumpAccount(oldWork);
                    Assert(pet.QuotaVisible&&pet.QuotaBubble.Title.Contains("DeepSeek"),"A late ChatGPT error cannot replace the selected DeepSeek loading state");
                    request.SetResult(new AccountQueryResult{Backend="deepseek",Balance=balance});PumpAccount(work);
                    oldWork=query.RunAsync();old=pending.Dequeue();c.SetBackend("codex");c.SetBackend("deepseek");work=query.RunAsync();request=pending.Dequeue();old.SetResult(new AccountQueryResult{Backend="deepseek",Balance=balance});PumpAccount(oldWork);
                    Assert(pet.QuotaVisible&&PanelVisuals((FrameworkElement)pet.QuotaBubble.Content).OfType<System.Windows.Controls.TextBlock>().Any(t=>t.Text=="正在查询…"),"Switching away and back still rejects an old result with the same backend name");
                    request.SetResult(new AccountQueryResult{Backend="deepseek",Balance=balance});PumpAccount(work);
                    c.SetBackend("codex");work=query.RunAsync();request=pending.Dequeue();request.SetException(new IOException("test error"));PumpAccount(work);Assert(pet.QuotaBubble.Title.Contains("剩余额度")&&pet.QuotaBubble.FiveRemainingText=="剩余 --","ChatGPT errors retain the quota layout rather than showing a DeepSeek error");
                    c.SetBackend("deepseek");work=query.RunAsync();request=pending.Dequeue();request.SetException(new IOException("test error"));PumpAccount(work);Assert(pet.QuotaBubble.Title.Contains("DeepSeek")&&PanelVisuals((FrameworkElement)pet.QuotaBubble.Content).OfType<System.Windows.Controls.TextBlock>().Any(t=>t.Text=="余额暂不可用"),"DeepSeek errors retain the balance layout");
                    work=query.RunAsync();request=pending.Dequeue();pet.ConsumeQuotaClick();request.SetResult(new AccountQueryResult{Backend="deepseek",Balance=balance});PumpAccount(work);Assert(!pet.QuotaVisible,"A dismissed account query cannot reopen when its result arrives");
                    work=query.RunAsync();request=pending.Dequeue();query.Dispose();request.SetResult(new AccountQueryResult{Backend="deepseek",Balance=balance});PumpAccount(work);Assert(!pet.QuotaVisible&&!pet.IsVisible&&!pet.QuotaBubble.IsVisible,"Disposal cancels pending feedback without showing a test window");
                    J.Save(Path.Combine(root,"account-query-properties.json"),P("routes",new[]{"ChatGPT: 5-hour+weekly quota","DeepSeek: account balance"},"menusSynchronized",true,"lateResultsIgnored",true,"earlyBubbleRetained",true,"windowsShown",false,"mouseInputUsed",false,"accountRequests",0));
                }
            }finally{query.Dispose();c.Dispose();pet.Close();app.Shutdown();SynchronizationContext.SetSynchronizationContext(context);}
        }
        private static void QueryTiming() {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var sprites=new Sprites();var pet=new PetWindow(sprites);
            var balance=BalanceSnapshot.Read(P("status","ready","value",new[]{P("currency","CNY","balance","18.256")},"bonusWallets",new[]{P("currency","CNY","balance","2")}));
            Func<string> text=()=>String.Join("\n",PanelVisuals((FrameworkElement)pet.QuotaBubble.Content).OfType<System.Windows.Controls.TextBlock>().Select(t=>t.Text));
            try {
                int originalClipMs=IdleEasterEggSchedule.Duration(sprites.Clips["quota-thinking"]);
                Assert(originalClipMs==2200,"The approved thinking loop retains all original frame timings");
                // Warm text layout so the first timing assertion does not include font startup.
                int ticket=pet.BeginQuotaInfo(true);Render((FrameworkElement)pet.QuotaBubble.Content,260,184,Path.Combine(root,"query-loading.png"));pet.DismissQuotaInfo();
                ticket=pet.BeginQuotaInfo(true);var start=System.Diagnostics.Stopwatch.StartNew();
                Assert(pet.QuotaVisible&&!pet.QuotaBubbleRevealed&&pet.CurrentAnimation=="quota-thinking","The thinking animation starts immediately before the bubble lead-in");
                Pump(70);Assert(!pet.QuotaBubbleRevealed,"The shortened lead-in retains a brief head-raising beat");
                Pump(210);long appeared=start.ElapsedMilliseconds;
                Assert(pet.QuotaBubbleRevealed&&pet.QuotaVisible&&appeared<800,"The bubble is requested near 200ms without waiting for the account response");
                Render((FrameworkElement)pet.QuotaBubble.Content,260,184,Path.Combine(root,"query-loading.png"));
                Assert(text().Contains("正在查询…")&&!text().Contains("余额暂不可用"),"Pending data is shown as loading, never as a fabricated amount or failure");
                pet.PlaceInteractionBubbles(new System.Windows.Rect(0,0,1920,1040),new System.Windows.Rect(600,400,192,254));double x=pet.QuotaBubble.Left,y=pet.QuotaBubble.Top;
                pet.PlaceInteractionBubbles(new System.Windows.Rect(0,0,1920,1040),new System.Windows.Rect(680,440,192,254));pet.ApplyDragFeedback("running-left");pet.FinishDragFeedback();
                Assert(pet.QuotaBubble.Left-x==80&&pet.QuotaBubble.Top-y==40&&pet.CurrentAnimation=="quota-thinking","The early loading bubble follows relocation while preserving the animation");
                Pump(500);var bubble=pet.QuotaBubble;long responseAt=start.ElapsedMilliseconds;
                Assert(pet.ShowBalanceResult(ticket,balance)&&Object.ReferenceEquals(bubble,pet.QuotaBubble)&&pet.QuotaBubbleRevealed,"A delayed result updates the existing early bubble in place");
                Render((FrameworkElement)pet.QuotaBubble.Content,260,184,Path.Combine(root,"query-result.png"));Assert(text().Contains("¥20.26")&&!text().Contains("正在查询…"),"The fresh response replaces loading with the correctly rounded balance");
                Pump(4500);Assert(pet.QuotaVisible,"Early bubble appearance does not shorten the original five-second result interval");
                Pump(750);Assert(!pet.QuotaVisible&&pet.CurrentAnimation=="idle","The interaction ends at the original result-plus-five-second deadline");
                long finishAt=start.ElapsedMilliseconds;
                Assert(responseAt-appeared>350&&finishAt-responseAt>=5000&&finishAt-responseAt<6000,"Only the bubble's visible span is extended; the original animation end stays unchanged");
                ticket=pet.BeginQuotaInfo(true);Assert(pet.ShowBalanceResult(ticket,balance)&&pet.QuotaBubbleRevealed,"A fast response can reveal the bubble immediately without waiting for the lead timer");
                Pump(260);Assert(!pet.ShowBalanceResult(ticket,balance)&&pet.QuotaVisible,"A delayed lead timer and duplicate response cannot restart an existing result");pet.DismissQuotaInfo();
                ticket=pet.BeginQuotaInfo(true);pet.ConsumeQuotaClick();Pump(260);Assert(!pet.QuotaVisible&&!pet.QuotaBubbleRevealed&&!pet.ShowBalanceResult(ticket,balance),"Dismissal before the early appearance prevents both the timer and late result reopening it");
                ticket=pet.BeginQuotaInfo(true);Pump(260);pet.ConsumeQuotaClick();Assert(!pet.QuotaVisible&&!pet.ShowBalanceError(ticket,"late error"),"Dismissal during loading returns to idle and rejects a late error");
                int old=pet.BeginQuotaInfo(true);ticket=pet.BeginQuotaInfo(true);Pump(260);Assert(!pet.ShowBalanceResult(old,balance)&&pet.ShowBalanceResult(ticket,balance),"Only the latest query can replace the early bubble contents");pet.DismissQuotaInfo();
                ticket=pet.BeginQuotaInfo(true);Pump(260);Assert(pet.ShowBalanceError(ticket,"test error"),"An error replaces loading using the original result timing");Render((FrameworkElement)pet.QuotaBubble.Content,260,184,Path.Combine(root,"query-error.png"));Assert(text().Contains("余额暂不可用")&&!text().Contains("正在查询…"),"A failed response is clearly distinguished from pending data");pet.DismissQuotaInfo();
                ticket=pet.BeginQuotaInfo();Pump(260);Render((FrameworkElement)pet.QuotaBubble.Content,260,184,Path.Combine(root,"quota-loading.png"));pet.QuotaBubble.RefreshCountdown(DateTime.UtcNow);
                Assert(pet.QuotaBubbleRevealed&&pet.QuotaBubble.FiveRemainingText=="查询中","The legacy quota path also appears early and retains its loading state on countdown refresh");
                var snapshot=QuotaSnapshot.Read(P("rateLimits",P("primary",P("usedPercent",27,"windowDurationMins",300),"secondary",P("usedPercent",61,"windowDurationMins",10080))));
                Assert(pet.ShowQuotaResult(ticket,snapshot)&&pet.QuotaBubble.FiveRemainingText=="剩余 73%","Quota results replace loading without changing percentage semantics");pet.DismissQuotaInfo();
                ticket=pet.BeginQuotaInfo(true);pet.Close();Pump(260);Assert(!pet.QuotaVisible&&!pet.QuotaBubbleRevealed&&!pet.ShowBalanceResult(ticket,balance),"Closing the pet cancels the pending appearance timer and rejects late account responses");
                Assert(!pet.IsVisible&&!bubble.IsVisible&&IdleEasterEggSchedule.Duration(sprites.Clips["quota-thinking"])==originalClipMs,"The complete timing checks stay off-screen and leave the character animation unchanged");
                J.Save(Path.Combine(root,"query-timing-properties.json"),P("bubbleTargetDelayMs",PetWindow.QueryBubbleDelayMs,"observedEarlyMs",appeared,"responseAtMs",responseAt,"endedAtMs",finishAt,"originalResultHoldMs",5000,"originalAnimationLoopMs",originalClipMs,"fullAnimationEndUnchanged",true,"windowsShown",false,"mouseInputUsed",false,"accountRequests",0));
            }finally{pet.Close();app.Shutdown();}
        }
        private static void CollapseStyle() {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            try {
                var button=Theme.CollapseButton(()=>{});
                var template=(System.Windows.Controls.ControlTemplate)System.Windows.Markup.XamlReader.Parse(System.Windows.Markup.XamlWriter.Save(button.Template));
                // Choose the same template states on a detached preview via Tag.
                // No mouse state, input event, window or production handler is used.
                foreach(var trigger in template.Triggers.OfType<System.Windows.Trigger>()) {
                    if(trigger.Property==UIElement.IsMouseOverProperty){trigger.Property=FrameworkElement.TagProperty;trigger.Value="hover";}
                    else if(trigger.Property==System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty){trigger.Property=FrameworkElement.TagProperty;trigger.Value="pressed";}
                }
                button.Template=template;
                foreach(string state in new[]{"normal","hover","pressed","normal"}) {
                    button.Tag=state;Render(button,76,34,Path.Combine(root,"collapse-"+state+".png"));
                    var border=PanelVisuals(button).OfType<System.Windows.Controls.Border>().First();var text=PanelVisuals(button).OfType<System.Windows.Controls.TextBlock>().First();
                    var background=((System.Windows.Media.SolidColorBrush)border.Background).Color;var foreground=((System.Windows.Media.SolidColorBrush)text.Foreground).Color;
                    var expected=state=="hover"?System.Windows.Media.Color.FromRgb(232,17,35):state=="pressed"?System.Windows.Media.Color.FromRgb(196,43,28):System.Windows.Media.Colors.White;
                    J.Save(Path.Combine(root,"collapse-"+state+"-colors.json"),P("background",background.ToString(),"foreground",foreground.ToString(),"windowShown",false,"mouseInputUsed",false));
                    Assert(background==expected&&(state=="normal"?foreground==((System.Windows.Media.SolidColorBrush)Theme.Ink).Color:foreground==System.Windows.Media.Colors.White),"Rendered collapse colors honor the "+state+" template state despite local button colors");
                }
            }finally{app.Shutdown();}
        }
        private static void WorkspaceBalance() {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            string fixture=Path.Combine(root,"workspace-reset-"+Guid.NewGuid().ToString("N")),defaults=Path.Combine(fixture,"Tasks"),custom=Path.Combine(fixture,"指定的项目目录很长");Directory.CreateDirectory(custom);
            string original=Path.Combine(custom,"keep.txt");File.WriteAllText(original,"ORIGINAL_FILE_"+Guid.NewGuid().ToString("N"));string bytes=File.ReadAllText(original);
            try {
                foreach(string backend in new[]{"deepseek","codex"}) {
                    string store=Path.Combine(fixture,"Data","session.json");
                    using(var c=new Controller(store,defaults,a=>a())) {
                        c.SetBackend(backend);c.NewChat(custom);c.ThreadId="previous-thread";c.Model="saved-model";c.Effort="high";c.Messages.Add(new ChatEntry("user","keep-history","test"));c.Save();
                        var pet=new PetWindow(new Sprites());var chat=new ChatWindow(c,pet);var surface=(FrameworkElement)chat.Content;
                        try {
                            c.AccountLabel=backend=="deepseek"?"DeepSeek · 账号登录":"ChatGPT · Plus";chat.Refresh();
                            foreach(var size in new[]{new System.Windows.Size(448,480),new System.Windows.Size(380,360)}) {
                                Render(surface,size.Width,size.Height,Path.Combine(root,"custom-workspace-"+backend+"-"+(int)size.Width+".png"));
                                var button=PanelVisuals(surface).OfType<System.Windows.Controls.Button>().First(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="取消指定任务路径");
                                var chooser=PanelVisuals(surface).OfType<System.Windows.Controls.Button>().First(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="选择任务文件夹");
                                var p=button.TranslatePoint(new System.Windows.Point(),surface);var f=chooser.TranslatePoint(new System.Windows.Point(),surface);
                                Assert(button.Visibility==Visibility.Visible&&button.IsEnabled&&p.X>=f.X&&p.X+button.ActualWidth<=f.X+chooser.ActualWidth&&J.B(chat.ComposerProperties(),"toolbarFits"),"The custom-path clear control fits within the compact folder box: "+backend+"-"+size.Width);
                            }
                            c.Status.Busy=true;chat.Refresh();Assert(!c.ResetWorkspace()&&c.Workspace==custom&&c.ThreadId=="previous-thread"&&c.Messages.Count==1,"Reset cannot change a running task's path or session: "+backend);c.Status.Busy=false;chat.Refresh();
                            Assert(c.ResetWorkspace()&&c.Workspace==defaults&&!c.HasCustomWorkspace&&c.ThreadId==""&&c.Messages.Count==0&&c.Model=="saved-model"&&c.Effort=="high","Reset restores default Tasks with a new conversation and retains model settings: "+backend);
                            Assert(Directory.Exists(defaults)&&File.ReadAllText(original)==bytes,"Reset never deletes or changes the previously selected folder: "+backend);
                            Render(surface,448,480,Path.Combine(root,"reset-workspace-"+backend+".png"));
                            var clear=PanelVisuals(surface).OfType<System.Windows.Controls.Button>().First(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="取消指定任务路径");Assert(clear.Visibility==Visibility.Collapsed&&!c.ResetWorkspace(),"The clear button disappears when the default path is active: "+backend);
                            Assert(!chat.IsVisible&&!pet.IsVisible,"Workspace reset checks display no window: "+backend);
                        } finally {chat.ExitWindow();pet.Close();}
                    }
                    using(var restored=new Controller(store,defaults,a=>a())){restored.SetBackend(backend);Assert(restored.Workspace==defaults&&!restored.HasCustomWorkspace&&restored.ThreadId==""&&restored.Model=="saved-model","A restart retains the reset path for this provider: "+backend);}
                }
                var wallet=new BalanceWallet{Currency="CNY",Paid=28.6962m};
                Assert(wallet.Amount(28.6962m)=="¥28.70"&&wallet.Amount(28.694m)=="¥28.69"&&wallet.Amount(28.695m)=="¥28.70"&&wallet.Amount(0.005m)=="¥0.01"&&wallet.Amount(0)=="¥0.00","Displayed money rounds half away from zero and always has two decimals");
                Assert(wallet.Paid==28.6962m,"Display rounding does not change the actual account amount");
                var bubble=new QuotaBubbleWindow();
                try {
                    foreach(bool multiple in new[]{false,true}) {
                        var balance=BalanceSnapshot.Read(P("status","ready","value",multiple?new[]{P("currency","CNY","balance","18.256"),P("currency","USD","balance","2.005")}:new[]{P("currency","CNY","balance","18.256")},"bonusWallets",new[]{P("currency","CNY","balance","2")}));
                        bubble.SetBalance(balance);var content=(FrameworkElement)bubble.Content;Render(content,bubble.Width,bubble.Height,Path.Combine(root,multiple?"balance-multiple-example.png":"balance-example.png"));
                        var texts=PanelVisuals(content).OfType<System.Windows.Controls.TextBlock>().Where(t=>t.IsVisible||t.Text=="DeepSeek 账户余额").ToList();
                        var title=PanelVisuals(content).OfType<System.Windows.Controls.TextBlock>().First(t=>t.Text=="DeepSeek 账户余额");var stack=(System.Windows.Controls.StackPanel)title.Parent;
                        var blockTop=stack.TranslatePoint(new System.Windows.Point(),content).Y;double middle=blockTop+stack.ActualHeight/2;
                        Assert(Math.Abs(middle-(bubble.Height-38)/2)<.5&&blockTop>=18&&bubble.Width==260&&bubble.Height==184,"The complete balance text block is centered in the unchanged bubble: "+(multiple?"multiple currencies":"one currency"));
                        Assert(balance.TotalText.Contains("¥20.26")&&balance.DetailText.Contains("¥18.26")&&balance.DetailText.Contains("¥2.00")&&(!multiple||balance.TotalText.Contains("USD 2.01")),"Totals and wallet details use two decimals independently per currency");
                        Assert(!bubble.IsVisible,"Balance rendering shows no window and queries no real account");
                    }
                }finally{bubble.Close();}
            }finally{app.Shutdown();}
        }
        private static IEnumerable<DependencyObject> PanelVisuals(DependencyObject element) {
            yield return element;
            for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(element);i++)foreach(var child in PanelVisuals(System.Windows.Media.VisualTreeHelper.GetChild(element,i)))yield return child;
        }
        private static void PanelUi() {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            string sandbox=Path.Combine(root,"panel-fixture-"+Guid.NewGuid().ToString("N")),tasks=Path.Combine(sandbox,"Tasks");Directory.CreateDirectory(tasks);
            var properties=new List<object>();
            try {
                foreach(string backend in new[]{"deepseek","codex"})using(var controller=new Controller(Path.Combine(sandbox,"Data","session.json"),tasks,a=>a())) {
                    controller.SetBackend(backend);controller.NewChat(tasks);controller.AccountLabel=backend=="deepseek"?"DeepSeek · 账号登录":"ChatGPT · Plus";
                    var model=new CodexModel{Id=backend=="deepseek"?"[\"deepseek-account\",\"deepseek-flash\"]":"gpt-6.1-sol",Name=backend=="deepseek"?"DeepSeek-V4.1-Flash":"GPT-6.1-Sol",Effort="high",SupportsImages=true,IsDefault=true};foreach(string effort in new[]{"off","low","high","max"})model.Efforts.Add(new CodexEffort{Id=effort,Description=effort});controller.Models.Add(model);controller.SetModel(model.Id);
                    var pet=new PetWindow(new Sprites());var chat=new ChatWindow(controller,pet);var surface=(System.Windows.FrameworkElement)chat.Content;
                    try {
                        foreach(var size in new[]{new System.Windows.Size(448,480),new System.Windows.Size(380,360),new System.Windows.Size(720,680)}) {
                            chat.Width=size.Width;chat.Height=size.Height;string name=backend+"-"+(int)size.Width;
                            Render(surface,size.Width,size.Height,Path.Combine(root,"panel-ui-"+name+".png"));
                            var visuals=PanelVisuals(surface).ToList();var buttons=visuals.OfType<System.Windows.Controls.Button>().ToList();
                            Func<string,System.Windows.Controls.Button> find=label=>buttons.First(b=>System.Windows.Automation.AutomationProperties.GetName(b)==label);
                            var folder=find("选择任务文件夹");var provider=find("切换对话服务");var start=find("新会话");
                            var f=folder.TranslatePoint(new System.Windows.Point(),surface);var p=provider.TranslatePoint(new System.Windows.Point(),surface);var n=start.TranslatePoint(new System.Windows.Point(),surface);
                            Assert(Math.Abs(f.Y-p.Y)<.1&&Math.Abs(p.Y-n.Y)<.1&&Math.Abs(folder.ActualWidth-96)<.1&&f.X+folder.ActualWidth<=p.X&&p.X+provider.ActualWidth<=n.X&&n.X+start.ActualWidth<=size.Width-20,"Compact folder, provider and new-chat controls share one row without overlap: "+name);
                            Assert(J.B(chat.ComposerProperties(),"toolbarFits"),"Outlined model and reasoning controls fit: "+name);
                            properties.Add(P("provider",backend,"width",size.Width,"folderWidth",folder.ActualWidth,"providerWidth",provider.ActualWidth,"headerY",f.Y,"toolbar",chat.ComposerProperties()));
                        }
                        chat.Width=448;chat.Height=480;controller.Messages.Add(new ChatEntry("assistant","preview","文件已整理完毕，可以继续向我发送任务。"));chat.Refresh();chat.PreviewDraft("请帮我整理一下这些资料。",new List<ChatAttachment>());
                        Render(surface,448,480,Path.Combine(root,"panel-ui-"+backend+"-conversation.png"));
                        Assert(!chat.IsVisible&&!pet.IsVisible&&!chat.Topmost&&pet.Topmost,"All UI checks remain off-screen and retain pet-only topmost behavior: "+backend);
                    } finally {chat.ExitWindow();pet.Close();}
                }
                var target=new Window { WindowStyle=WindowStyle.None, AllowsTransparency=true };var fade=new PanelFade();int shown=0,hidden=0;
                fade.Reveal(target,false,()=>shown++);var initial=fade.Clock;initial.Controller.Pause();Pump(20);initial.Controller.SeekAlignedToLastTick(TimeSpan.FromMilliseconds(35),System.Windows.Media.Animation.TimeSeekOrigin.BeginTime);
                Assert(shown==1&&target.Opacity>0&&target.Opacity<1,"Showing begins transparent and progresses through intermediate opacity");
                double before=target.Opacity;fade.Conceal(target,()=>hidden++);var oldHide=fade.Clock;oldHide.Controller.Pause();Pump(10);oldHide.Controller.SeekAlignedToLastTick(TimeSpan.FromMilliseconds(55),System.Windows.Media.Animation.TimeSeekOrigin.BeginTime);
                Assert(hidden==0&&target.Opacity>0&&target.Opacity<before,"Hiding fades from the current opacity and defers the hide callback");
                fade.Conceal(target,()=>hidden++);Assert(Object.ReferenceEquals(oldHide,fade.Clock),"Repeated collapse requests do not restart an active fade-out");
                double partial=target.Opacity;fade.Reveal(target,true,()=>shown++);var reopened=fade.Clock;reopened.Controller.Pause();
                Assert(!fade.Hiding&&Math.Abs(target.Opacity-partial)<.01&&shown==1,"Reopening reverses a fade without flashing or showing a duplicate window");
                oldHide.Controller.SkipToFill();Pump(20);Assert(hidden==0,"An obsolete fade-out cannot hide a reopened panel");
                reopened.Controller.SkipToFill();Pump(25);Assert(fade.Clock==null&&Math.Abs(target.Opacity-1)<.001,"Fade-in finishes fully opaque and releases its animation clock");
                fade.Reveal(target,true,()=>shown++);Assert(fade.Clock==null&&shown==1,"Opening an already visible panel only retains its current display");
                fade.Conceal(target,()=>hidden++);fade.Clock.Controller.SkipToFill();Pump(25);Assert(hidden==1&&target.Opacity==0&&!fade.Hiding,"Completed fade-out hides exactly once");
                fade.Reveal(target,false,()=>shown++);fade.Clock.Controller.SkipToFill();Pump(25);Assert(shown==2&&target.Opacity==1,"A hidden panel can reopen after a completed fade-out");
                fade.Conceal(target,()=>hidden++);var closingClock=fade.Clock;fade.Cancel(target);closingClock.Controller.SkipToFill();Pump(20);Assert(hidden==1&&target.Opacity==1&&fade.Clock==null,"Shutdown cancels pending fade callbacks safely");
                Assert(!target.IsVisible,"Animation timing checks never display a native window");target.Close();
                J.Save(Path.Combine(root,"panel-ui-properties.json"),P("layouts",properties,"fadeInMs",PanelFade.RevealMs,"fadeOutMs",PanelFade.ConcealMs,"rapidToggleVerified",true,"windowsShown",false,"mouseInputUsed",false,"accountRequests",0));
            } finally {app.Shutdown();}
        }
        private static async Task HarnessPrepare() {
            var models = await HarnessRuntime.AuxiliaryAsync("catalog", root, Path.Combine(root,"bridge"), null);
            J.Save(Path.Combine(root,"harness-models.json"),models);
            Console.WriteLine("Account models: " + J.A(models).Length);
            var ready = await HarnessRuntime.AuxiliaryAsync("audio-prepare", root, Path.Combine(root,"bridge"), null);
            J.Save(Path.Combine(root,"audio-preparation.json"),ready);
            Console.WriteLine("Local speech resources ready: " + J.B(ready,"ready"));
        }
        private static void HarnessLogic() {
            DateTime monday = new DateTime(2026,10,19,1,0,0,DateTimeKind.Utc);
            Assert(PricingPeriod.Peak(monday) == true && PricingPeriod.Peak(monday.AddSeconds(-1)) == false, "Peak starts exactly at 09:00 China time");
            Assert(PricingPeriod.Peak(monday.AddHours(3)) == false && PricingPeriod.Peak(monday.AddHours(5)) == true && PricingPeriod.Peak(monday.AddHours(9)) == false,"Noon and evening boundaries use the published two peak windows");
            Assert(PricingPeriod.Peak(new DateTime(2026,10,10,1,0,0,DateTimeKind.Utc)) == false,"A Saturday make-up workday still uses weekend off-peak pricing");
            Assert(PricingPeriod.Peak(new DateTime(2026,10,1,1,0,0,DateTimeKind.Utc)) == false && PricingPeriod.Peak(new DateTime(2026,2,23,1,0,0,DateTimeKind.Utc)) == false,"Chinese holiday weekdays use off-peak pricing");
            Assert(PricingPeriod.Peak(new DateTime(2027,1,4,1,0,0,DateTimeKind.Utc)) == null,"An unconfirmed future holiday calendar never silently claims a peak period");
            Assert(PricingPeriod.Next(monday.AddMinutes(-1)).Contains("1分钟后转峰期"),"The next-period countdown is precise at a boundary");
            var balance=BalanceSnapshot.Read(P("status","ready","value",new[]{P("currency","CNY","balance","1.2345"),P("currency","USD","balance","2")},"bonusWallets",new[]{P("currency","CNY","balance","0E-16")}));
            Assert(balance.Wallets.Count==2 && balance.Wallets[0].Paid==1.2345m && balance.Wallets[0].Bonus==0,"Wallet parsing preserves decimal amounts and never converts or merges currencies");
            bool missing=false;try{BalanceSnapshot.Read(null);}catch{missing=true;} Assert(missing,"Missing account data is unavailable, never a zero balance");
            var catalog=DeepSeekClient.ReadModels(new[]{P("id","vision","name","Vision","inputModalities",new[]{"text","image"},"reasoning",P("defaultEffort","high","efforts",new[]{P("id","off"),P("id","low"),P("id","high")})),P("id","plain","inputModalities",new[]{"text"})},true);
            Assert(catalog.Count==2&&catalog[0].SupportsImages&&!catalog[1].SupportsImages&&catalog[0].Efforts.Count==3&&catalog[0].Effort=="high","Harness model parsing preserves per-model image admission and reasoning defaults");
            using(var client=new DeepSeekClient(root)){bool blocked=false;try{client.RequestAsync("turn/start",P("model","[\"deepseek-official\",\"deepseek-flash\"]"),100).GetAwaiter().GetResult();}catch(IOException){blocked=true;}Assert(blocked,"An API-key route is rejected before any native request");}
            string invalidAudio=Path.Combine(root,"invalid-audio.wav");File.WriteAllText(invalidAudio,"not a recording");bool badAudio=false;
            try{AudioInput.TranscribeAsync(invalidAudio,root,Path.Combine(root,"bad-audio"),CancellationToken.None).GetAwaiter().GetResult();}catch(IOException){badAudio=true;}Assert(badAudio&&Directory.GetFiles(Path.Combine(root,"bad-audio"),"audio-*.wav").Length==0,"Malformed audio fails clearly and its temporary conversion is cleaned up");
            bool cancelled=false;var cancel=new CancellationTokenSource();cancel.Cancel();try{AudioInput.TranscribeAsync(invalidAudio,root,Path.Combine(root,"bad-audio"),cancel.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}cancel.Dispose();Assert(cancelled,"Cancelling audio preparation never submits a model prompt");
            string data=Path.Combine(root,"backend-isolation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(data);
            string store=Path.Combine(data,"session.json");
            using(var c=new Controller(store,root,a=>a())){
                c.ThreadId="original-codex";c.Model="old-model";c.Messages.Add(new ChatEntry("user","c","legacy"));c.Save();
                Assert(c.SetBackend("deepseek") && c.ThreadId=="" && c.Messages.Count==0,"DeepSeek starts with separate history and model settings");
                c.ThreadId="deepseek-test";c.Model="[\"deepseek-account\",\"deepseek-flash\"]";c.Effort="low";c.Messages.Add(new ChatEntry("user","d","new"));c.Save();
                c.SetBackend("codex"); Assert(c.ThreadId=="original-codex" && c.Messages[0].Text=="legacy","Switching back restores the original Codex conversation");
                c.SetBackend("deepseek");Assert(c.ThreadId=="deepseek-test" && c.Effort=="low" && c.Messages[0].Text=="new","Restart state restores the DeepSeek conversation and reasoning choice");
                c.Status.Busy=true; Assert(!c.SetBackend("codex"),"A running task cannot switch provider");c.Status.Busy=false;
            }
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown}; var pet=new PetWindow(new Sprites()); var bubble=new QuotaBubbleWindow();
            try {
                bubble.SetBalance(balance);Render((System.Windows.FrameworkElement)bubble.Content,bubble.Width,bubble.Height,Path.Combine(root,"balance-example.png"));
                Assert(bubble.Width==260 && bubble.Height==184 && !bubble.IsVisible,"The balance display keeps the approved bubble size without showing a window");
                int ticket=pet.BeginQuotaInfo();Assert(pet.ShowBalanceResult(ticket,balance)&&pet.QuotaVisible,"A balance query reuses the approved thinking animation and timed result state");
                pet.ApplyDragFeedback("running-left");pet.FinishDragFeedback();Assert(pet.QuotaVisible&&pet.CurrentAnimation=="quota-thinking","Dragging protects the balance animation");pet.ConsumeQuotaClick();Assert(!pet.QuotaVisible&&pet.CurrentAnimation=="idle","Click dismissal restores idle and follows the existing bubble fade path");
                var controller=new Controller(Path.Combine(data,"ui.json"),root,a=>a());controller.SetBackend("deepseek");var chat=new ChatWindow(controller,pet);
                try{
                    controller.Models.Add(new CodexModel{Id="[\"deepseek-account\",\"deepseek-flash\"]",Name="DeepSeek-V4.1-Flash",Effort="low",SupportsImages=true,IsDefault=true});controller.Models[0].Efforts.Add(new CodexEffort{Id="low",Description="低"});controller.SetModel(controller.Models[0].Id);
                    string latest=Directory.GetDirectories(root,"Harness 完整测试 *").OrderBy(p=>p).LastOrDefault();
                    string previewAudio=latest==null?invalidAudio:Path.Combine(latest,"附件 原件","语音 样本.wav");
                    controller.Messages.Clear();controller.Messages.Add(new ChatEntry("user","example","请帮我整理附件中的资料。"));chat.Refresh();
                    chat.PreviewDraft("请帮我读取并整理这些文件。",new[]{AttachmentInput.FromFile(previewAudio)});
                    Render((System.Windows.FrameworkElement)chat.Content,448,480,Path.Combine(root,"deepseek-chat-example.png"));Assert(!chat.Topmost&&pet.Topmost&&!chat.IsVisible,"Provider UI stays off-screen, with only the pet kept on top");
                    Assert(J.B(chat.ComposerProperties(),"toolbarFits"),"The DeepSeek model and reasoning controls fit the default composer");
                    Render((System.Windows.FrameworkElement)chat.Content,380,360,Path.Combine(root,"deepseek-chat-minimum.png"));Assert(J.B(chat.ComposerProperties(),"toolbarFits"),"The DeepSeek model name trims correctly at minimum window width");
                    balance=BalanceSnapshot.Read(P("status","ready","value",new[]{P("currency","CNY","balance","18.2583")},"bonusWallets",new[]{P("currency","CNY","balance","2")}));bubble.SetBalance(balance);Render((System.Windows.FrameworkElement)bubble.Content,bubble.Width,bubble.Height,Path.Combine(root,"balance-example.png"));
                }finally{chat.ExitWindow();controller.Dispose();}
            }finally{bubble.Close();pet.Close();app.Shutdown();}
        }
        private static string Reply(Controller c) { return String.Join("\n",c.Messages.Where(m=>m.Role=="assistant").Select(m=>m.Text)); }
        private static async Task HarnessAudioCheck() {
            string run=Directory.GetDirectories(root,"Harness 完整测试 *").OrderBy(p=>p).Last();
            foreach(string extension in new[]{"wav","mp3"}) {
                string transcript=await AudioInput.TranscribeAsync(Path.Combine(run,"附件 原件","语音 样本."+extension),root,Path.Combine(root,"audio-check"),CancellationToken.None);
                Console.WriteLine(extension+": "+transcript);
                Assert(transcript.ToLowerInvariant().Contains("pineapple"),"Real "+extension+" decoding and local speech recognition");
            }
            J.Save(Path.Combine(root,"harness-audio-check-test.json"),P("ok",true,"checks",checks));
        }
        private static async Task HarnessLive() {
            string run=Path.Combine(root,"Harness 完整测试 "+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(run);
            string work=Path.Combine(run,"任务 空间");Directory.CreateDirectory(work);
            string input=Path.Combine(run,"附件 原件");Directory.CreateDirectory(input);
            string marker="READ_"+Guid.NewGuid().ToString("N"),context="MEMORY_"+Guid.NewGuid().ToString("N").Substring(0,10),fresh="FRESH_"+Guid.NewGuid().ToString("N");
            File.WriteAllText(Path.Combine(work,"project.md"),marker,new UTF8Encoding(false));
            string text=Path.Combine(input,"说明 文档.txt");File.WriteAllText(text,"attachment-value="+context,new UTF8Encoding(false));
            string doc=Path.Combine(input,"资料.docx");
            using(var zip=ZipFile.Open(doc,ZipArchiveMode.Create))using(var writer=new StreamWriter(zip.CreateEntry("word/document.xml").Open()))writer.Write("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>文档校验：橙色星星</w:t></w:r></w:p></w:body></w:document>");
            string image=Path.Combine(input,"视觉样本.png"),imageCode="V"+new Random().Next(10000,99999);
            using(var bitmap=new Bitmap(640,360))using(var g=Graphics.FromImage(bitmap))using(var font=new Font("Arial",44,System.Drawing.FontStyle.Bold)){
                g.Clear(System.Drawing.Color.White);g.DrawString(imageCode,font,System.Drawing.Brushes.Black,35,25);g.FillRectangle(System.Drawing.Brushes.Blue,45,150,130,130);g.FillEllipse(System.Drawing.Brushes.Red,225,150,130,130);g.FillPolygon(System.Drawing.Brushes.Green,new[]{new System.Drawing.Point(430,280),new System.Drawing.Point(510,140),new System.Drawing.Point(590,280)});bitmap.Save(image,System.Drawing.Imaging.ImageFormat.Png);
            }
            string wave=Path.Combine(input,"语音 样本.wav"),mp3=Path.Combine(input,"语音 样本.mp3");
            dynamic voice=Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpVoice"));dynamic stream=Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpFileStream"));
            try { stream.Format.Type=18;stream.Open(wave,3,false);voice.AudioOutputStream=stream;voice.Rate=-1;voice.Speak("The secret fruit is pineapple. Please write the word pineapple to the result file.",0);stream.Close(); }
            finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(voice);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(stream); }
            using(var reader=new WaveFileReader(wave))MediaFoundationEncoder.EncodeToMp3(reader,mp3,128000);
            string store=Path.Combine(run,"session.json");var observed=new List<string>();int permissionCount=0;
            using(var c=new Controller(store,work,a=>a())) {
                c.SetBackend("deepseek");await c.ConnectAsync();
                c.ApprovalRequested+=r=>{permissionCount++;c.Answer(r,P("decision","decline"));};
                Action observe=()=>{c.Client.Event+=(s,e)=>{lock(observed)observed.Add(e.Method);};};observe();
                Assert(c.AccountLabel.StartsWith("DeepSeek")&&c.Models.Count>0&&!c.Connecting,"The production adapter connects to the existing DeepSeek account");
                J.Save(Path.Combine(run,"models.json"),c.Models.Select(m=>P("id",m.Id,"name",m.Name,"images",m.SupportsImages,"efforts",m.Efforts.Select(e=>e.Id).ToArray())).ToArray());
                var flash=c.Models.Find(m=>m.SupportsImages);Assert(flash!=null&&flash.Efforts.Count>0,"The live catalog supplies image capability and supported reasoning levels per model");
                c.SetModel(flash.Id);Assert(c.SetEffort("low"),"Flash accepts the advertised low reasoning level");
                Assert(await c.SendAsync("请只回复 37+58 的结果，并记住本会话标记 "+context+"。无需工具。"),"A text-only turn is accepted");await Wait(c,180000);Assert(c.Status.State==PetState.Completed&&Reply(c).Contains("95"),"A real text reply arrives through the production controller");
                Assert(await c.SendAsync("请处理所附 txt 和 docx，回复 docx 中的校验短语。还必须实际读取当前工作文件夹的 project.md（内容未在提示中给出），将原样内容加 |ATTACH_OK 写入 result.txt，UTF-8，无多余字符。所有操作仅限当前任务与所附快照，不访问网络。",new[]{AttachmentInput.FromFile(text),AttachmentInput.FromFile(doc)}),"Text and Word attachments are submitted together");
                await Wait(c,240000);Assert(c.Status.State==PetState.Completed&&File.Exists(Path.Combine(work,"result.txt"))&&File.ReadAllText(Path.Combine(work,"result.txt")).Trim()==marker+"|ATTACH_OK","Native file tools read unknown local bytes and return the correct output file");
                Assert(Reply(c).Contains("橙色星星"),"The attached Word document is read correctly");
                var user=c.Messages.FindLast(m=>m.Role=="user");Assert(user.Attachments.Count==2&&user.Attachments[0].Path!=text&&File.ReadAllText(user.Attachments[0].Path).Contains(context),"Chinese-space file paths use stable attachment snapshots");
                string session=c.ThreadId;c.Client.Dispose();File.WriteAllText(Path.Combine(work,"project.md"),fresh,new UTF8Encoding(false));await c.ConnectAsync();observe();
                Assert(await c.SendAsync("重新用工具读取 project.md，将最新内容加 |RESUME_OK 写入 resume.txt。回复之前让你记住的 MEMORY_ 会话标记。只访问当前工作文件夹。"),"A disconnected session reconnects and accepts a follow-up");await Wait(c,240000);
                Assert(c.ThreadId==session&&c.Status.State==PetState.Completed&&File.ReadAllText(Path.Combine(work,"resume.txt")).Trim()==fresh+"|RESUME_OK"&&Reply(c).Contains(context),"The same native Harness session retains context and reads changed disk content");
                Assert(await c.SendAsync("请直接看图，先写出图中顶部的字母数字代码及三个彩色形状。在当前工作文件夹保存 vision.txt，内容为顶部代码；再用本地工具将所附原图顺时针旋转90度保存为 rotated.png，保留全部像素。不要从文件名猜内容，只处理当前文件夹和附件。",new[]{AttachmentInput.FromFile(image)}),"An image is submitted through native ACP image input");await Wait(c,300000);
                Assert(c.Status.State==PetState.Completed&&File.ReadAllText(Path.Combine(work,"vision.txt")).Trim()==imageCode,"The model visually recognizes a random code absent from the prompt and filename");
                using(var original=new Bitmap(image))using(var rotated=new Bitmap(Path.Combine(work,"rotated.png"))){original.RotateFlip(RotateFlipType.Rotate90FlipNone);Assert(rotated.Width==360&&rotated.Height==640&&rotated.GetPixel(200,80).ToArgb()==original.GetPixel(200,80).ToArgb()&&rotated.GetPixel(20,20).ToArgb()==original.GetPixel(20,20).ToArgb(),"The model processes the actual image file and returns a verified rotated PNG");}
                Assert(await c.SendAsync("请根据两个语音附件的本地转写，找出 secret fruit，并只将该英文水果单词写入 audio-result.txt。只在任务文件夹处理。",new[]{AttachmentInput.FromFile(wave),AttachmentInput.FromFile(mp3)}),"WAV and MP3 attachments pass decoding and local transcription before submission");await Wait(c,240000);
                Assert(c.Status.State==PetState.Completed&&File.ReadAllText(Path.Combine(work,"audio-result.txt")).Trim().ToLowerInvariant()=="pineapple","Recognized speech reaches the model and produces the expected file");
                user=c.Messages.FindLast(m=>m.Role=="user");Assert(user.Attachments.All(a=>a.ExtractedText.ToLowerInvariant().Contains("pineapple")&&a.ReadHint.Contains("本地")),"Both audio formats have real local transcripts and truthful attachment labels");
                var pro=c.Models.Find(m=>!m.SupportsImages);if(pro!=null){c.SetModel(pro.Id);c.SetEffort("low");Assert(await c.SendAsync("只回复 PRO_SWITCH_OK，无需工具。"),"The other account model accepts a real model switch");await Wait(c,240000);Assert(c.Status.State==PetState.Completed&&Reply(c).Contains("PRO_SWITCH_OK"),"The switched model completes a turn in the same conversation");}c.SetModel(flash.Id);c.SetEffort("low");
                Assert(await c.SendAsync("仅为停止按钮测试，请在当前文件夹用本地命令等待30秒（Start-Sleep），然后写 cancel-should-not-exist.txt。不要提前回复，不访问其他目录或网络。"),"A cancellable native tool task starts");
                var watch=System.Diagnostics.Stopwatch.StartNew();while(c.Status.State!=PetState.Working&&c.Busy&&watch.ElapsedMilliseconds<40000)await Task.Delay(100);
                Assert(c.Busy&&c.Status.State==PetState.Working,"Native tool events synchronize the working state");
                string beforeThread=c.ThreadId;int count=c.Messages.Count;var bal=await c.ReadBalanceAsync();
                Assert(bal.Wallets.Count>0&&c.Busy&&c.ThreadId==beforeThread&&c.Messages.Count==count,"A live account-balance query runs alongside work without adding a model turn or changing the conversation");
                await c.StopAsync();await Wait(c,45000);Assert(c.Status.State==PetState.Interrupted&&!File.Exists(Path.Combine(work,"cancel-should-not-exist.txt")),"Cancellation stops the private native task without reporting success");
                Assert(await c.SendAsync("只回复 AFTER_STOP_OK，无需工具。"),"The conversation can send again after cancellation");await Wait(c,180000);Assert(c.Status.State==PetState.Completed&&Reply(c).Contains("AFTER_STOP_OK"),"The resumed conversation completes after cancellation");
                Assert(permissionCount==0,"Routine workspace operations needed no permission bypass");
                Assert(observed.Contains("item/reasoning/summaryTextDelta")&&observed.Contains("item/agentMessage/delta")&&observed.Contains("item/started"),"Thinking, native tools, and replies all reach the production state adapter");
                J.Save(Path.Combine(root,"harness-live-summary.json"),P("workspace",work,"sessionId",session,"text",true,"txt",true,"docx",true,"imageRecognition",true,"imageProcessing",true,"audioWavLocalTranscription",true,"audioMp3LocalTranscription",true,"nativeAudioInput",false,"accountBalance",true,"cancellation",true,"reconnect",true,"apiKeyUsed",false,"windowsShown",false,"mouseInputUsed",false,"permissionBypass",false));
            }
        }
    }
}
