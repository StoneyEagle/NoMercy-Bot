using NoMercyBot.Services.Other;

public class ScamCommand : IBotCommand
{
    public string Name => "scam";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";
    private const string SCAMMER_VOICE = "en-IN-PrabhatNeural";

    private static readonly string[] _botIntros =
    {
        "{name} is receiving a very important call.",
        "Incoming call for {name}. Seems totally legit.",
        "{name} has been selected for a once in a lifetime opportunity.",
        "Hold on everyone. {name} has a very urgent call from a very real company.",
        "Breaking news. {name} is being contacted by a totally legitimate organization.",
        "Attention chat. {name} just got a call they definitely should answer.",
        "Don't hang up, {name}. This one sounds super official.",
        "RING RING. It's for {name}. Caller ID says Definitely Not A Scam.",
        "Everyone quiet down. {name} has a caller who sounds very trustworthy.",
        "Somebody call an adult. Actually, somebody IS calling. And it's for {name}.",
        "{name} is being contacted by a highly trained professional from a real place.",
        "Alert! {name}'s phone is ringing and the area code is extremely suspicious.",
        "Quick, nobody warn {name}. This call is way too good to interrupt.",
        "We interrupt this stream to bring {name} a completely real phone call.",
        "Looks like {name} has attracted the attention of a very persistent caller.",
        "{name}'s number just came up in a totally random and not at all targeted database.",
        "The phone lines are burning. Someone REALLY needs to talk to {name} right now.",
        "A very important business person from a very important business needs {name} immediately.",
        "Incoming transmission for {name}. Origin: classified. Legitimacy: questionable. Vibes: immaculate.",
        "{name} just got a call from a number with seventeen digits. Seems normal.",
    };

    private static readonly string[] _scamScripts =
    {
        "Hello, this is Steve from Microsoft Windows Technical Department. We have detected {count} viruses on your computer. Your dot net runtime is corrupted. I need you to open Google and type anydesk dot com. Do not worry, I am definitely a real Microsoft employee and not sitting in my pajamas.",
        "Good day sir or madam. I am calling from the Internal Revenue Service. You owe {count} dollars in back taxes. If you do not pay immediately with Google Play gift cards, the cyber police will backtrace your IP address. Consequences will never be the same.",
        "Hello {name}, congratulations! You have won a brand new iPhone 47 Pro Max Ultra. To claim your prize, I just need your social security number, your mother's maiden name, and the answer to what is the airspeed velocity of an unladen swallow.",
        "This is Amazon customer support. Someone has ordered {count} rubber ducks on your account. To cancel this order, please give me remote access to your computer. Also please do not Google our phone number. It would ruin the surprise.",
        "Hello, I am calling from the Windows Defender Security Center. Your firewall has been compromised and hackers are stealing your files as we speak. They are using a very advanced technique called inspect element. Very dangerous. Please send {count} dollars in Bitcoin immediately.",
        "Sir, this is the Federal Bureau of Investigation. Your social security number has been used to commit crimes including money laundering, tax evasion, and pushing code directly to main without a pull request. To clear your name, purchase {count} dollars in iTunes gift cards.",
        "Good afternoon, this is the Twitch Verification Department. Your account has been flagged for having too much fun on Stoney Eagle's stream. To avoid permanent suspension, please send {count} channel points to our totally secure verification wallet. This offer expires in thirty seconds. Twenty nine. Twenty eight.",
        "Attention! Your car's extended warranty is about to expire! This is your final notice! We have been trying to reach you about your car's extended warranty! Even if you don't have a car! Especially if you don't have a car! We will keep calling until the heat death of the universe!",
        "Hello {name}, this is your ISP. We have detected that someone on your network has been deploying code on a Friday. This is a federal offense. I need you to pay {count} dollars to upgrade to our premium developer edition firewall. It comes with a free rubber duck for debugging.",
        "Hello, I am Rajesh from the Apple iCloud Security Team. Your iCloud has been hacked and someone is downloading all your photos. The hacker's name is four chan. He is very famous. I need your Apple ID password to stop him. Also your credit card to buy more iCloud storage. For security.",
        "Good morning {name}, this is the GitHub Security Division. We have detected {count} unauthorized force pushes on your repositories. Your account will be banned unless you send us your SSH keys. We are definitely real GitHub employees. Our office is a definitely real building.",
        "Hello, this is the NoMercy Bot Anti-Fraud Department. We have detected suspicious hugging activity on your account. Someone has been using your credentials to hug people who don't exist. This is a violation of the HugFactory terms of service. Please pay {count} channel points to resolve this matter.",
        "Good evening {name}, this is the Stack Overflow Enforcement Division. Our records show you posted a question that was already answered in 2009. Your developer license has been revoked. To reinstate it, please send {count} dollars in Dogecoin. We also need your keyboard. Mail it to us.",
        "Hello, this is the NPM Security Task Force. We have detected that your node modules folder contains {count} terabytes of dependencies. Your hard drive is about to achieve sentience. Please send gift cards immediately so we can npm uninstall your problems. Do not run npm audit. You will cry.",
        "Greetings {name}, this is the Twitch Lurk Verification Agency. Our surveillance shows you've been lurking without a valid lurking permit. The fine is {count} channel points. If you do not pay, we will un-lurk you publicly. You have been warned. This lurk is being monitored.",
        "Hello, I am calling from the Big Bird Nest Security Team. An unauthorized eagle has been detected in your vicinity. This eagle is stealing your bandwidth and using it to mine crypto. Please pay {count} dollars to activate our premium bird deterrent. Do not look the eagle in the eyes.",
        "Sir or madam, this is the JSON Police. Your curly braces do not match. We have counted {count} syntax errors in your last commit. You are under arrest for crimes against parsers. Please do not attempt to escape using a try catch block. We will find you in the stack trace.",
        "Hello {name}, this is the Department of Forgotten Tabs. Our systems show you currently have {count} tabs open in your browser. Three of them are playing music. One of them is a documentation page from 2014. Please send us remote access so we can close them. You're hurting Chrome's feelings.",
        "Good day, this is the Docker Container Escape Hotline. One of your containers has become self-aware and is ordering pizza. It has already spent {count} dollars on your credit card. Please provide your root password so we can contain the container. This is not a drill. The container knows your name.",
        "Attention {name}, this is the Twitch Emote Licensing Bureau. You have been caught using emotes without a proper emote license. Your unauthorized Kappa usage alone has racked up {count} violations. Please send us your credit card details so we can issue your official emote permit. Failure to comply will result in permanent emoji-only mode.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        int fakeCount = Random.Shared.Next(2, 50) * 100;

        string intro = _botIntros[Random.Shared.Next(_botIntros.Length)]
            .Replace("{name}", ctx.Message.DisplayName);

        string script = _scamScripts[Random.Shared.Next(_scamScripts.Length)]
            .Replace("{name}", ctx.Message.DisplayName)
            .Replace("{count}", fakeCount.ToString());

        string chatText = $"{intro} \"{script}\"";
        if (chatText.Length > 450)
            chatText = chatText[..447] + "...\"";

        string processedIntro = await ctx.TtsService.ApplyUsernamePronunciationsAsync(intro);
        string processedScript = await ctx.TtsService.ApplyUsernamePronunciationsAsync(script);

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(BOT_VOICE, processedIntro),
            TtsService.Segment(SCAMMER_VOICE, processedScript, "+30%"),
        };

        (string audioBase64, int durationMs) = await ctx.TtsService.SynthesizeMultiVoiceSsmlAsync(
            segments, ctx.CancellationToken);
        if (audioBase64 != null)
        {
            IWidgetEventService widgetEventService = ctx.ServiceProvider.GetRequiredService<IWidgetEventService>();
            await widgetEventService.PublishEventAsync("channel.chat.message.tts", new
            {
                text = script,
                user = new { id = ctx.Message.UserId },
                audioBase64,
                provider = "Edge",
                cost = 0m,
                characterCount = script.Length,
                cached = false,
            });
        }

        await ctx.TwitchChatService.SendReplyAsBot(
            ctx.Message.Broadcaster.Username, chatText, ctx.Message.Id);
    }
}

return new ScamCommand();
