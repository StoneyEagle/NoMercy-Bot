using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;

/// <summary>
/// The official IHugFactory implementation.
/// Chat voted on the name. Democracy was a mistake.
/// </summary>
public class HugFactory : IBotCommand
{
    public string Name => "hug";
    public CommandPermission Permission => CommandPermission.Everyone;

    // Target specified - {name} hugs {target}
    private static readonly string[] _hugTemplates =
    {
        "{name} aggressively bear-hugs {target}. {target} may never recover.",
        "{name} wraps {target} in a hug so tight their eyes bug out. It's fine. This is fine.",
        "{name} lunges at {target} with open arms. {target} had no time to run.",
        "{name} gives {target} one of those hugs that lasts just a little too long. Everyone's uncomfortable now.",
        "{name} hugs {target} with the desperation of someone who just found out the stream is ending.",
        "{name} delivers a certified factory-grade hug to {target}. Quality assured. No refunds.",
        "{name} sneaks up behind {target} and delivers a surprise hug. HR has been notified.",
        "{name} hugs {target} so hard their soul briefly leaves their body. Worth it.",
        "{name} initiates hug protocol with {target}. Target acquired. Embrace deployed.",
        "{name} gives {target} a hug that can only be described as 'aggressively wholesome.'",
        "{name} picks up {target} in a hug and refuses to put them down. This is their life now.",
        "{name} hugs {target} like they're trying to squeeze the last bit of toothpaste out of the tube.",
        "{name} and {target} share a hug so powerful it causes a brief disturbance in the Twitch servers.",
        "{name} wraps {target} in a warm embrace. Chat collectively says 'aww' but secretly judges them both.",
        "{name} hugs {target} with the intensity of a thousand subs. The hug train has left the station.",
        "{name} attempts to hug {target}. Critical hit! It's super effective!",
        "{name} gives {target} a group hug, but it's just the two of them. Somehow that makes it weirder.",
        "{name} hugs {target}. A single tear rolls down chat's face. Beautiful.",
        "{name} tackles {target} with a flying hug. {target}'s chiropractor sends a thank-you note.",
        "{name} gives {target} one of those movie-quality slow-motion reunion hugs. Someone play the music.",
        "{name} engulfs {target} in a hug so cozy they both almost fall asleep on stream.",
        "{name} hugs {target} and whispers 'I main Jigglypuff.' {target} has never felt more violated.",
        "{name} initiates the HugFactory assembly line. {target} is the first product. No quality control needed.",
        "{name} deploys a tactical hug on {target}. Resistance is futile. The hug assimilates all.",
        "{name} gives {target} a hug backed by ISO 9001 certification. Only the finest hugs leave this factory.",
    };

    // No target - self-hug or nudge to specify someone
    private static readonly string[] _noTargetTemplates =
    {
        "{name} tries to hug... the air? You gotta specify someone, genius. !hug @username",
        "{name} opens their arms wide and hugs... absolutely nobody. Tragic. Try !hug @someone",
        "{name} is standing there with open arms like a lost penguin. Pick a target! !hug @username",
        "{name} attempts a self-hug. It's as sad as it sounds. Maybe try hugging someone else? !hug @username",
        "{name} is out here hugging the void. The void does not hug back. Try !hug @username",
        "{name} deployed a hug with no recipient. Packet dropped. Try !hug @username",
        "{name} just sent a hug into /dev/null. Nobody received it. !hug @username",
        "{name} is standing there like a loading screen that never finishes. Pick someone! !hug @username",
        "{name} threw a hug into the Twitch void. Even Big Bird looked away in secondhand embarrassment. !hug @username",
        "{name} casts Hug. Target: undefined. NullReferenceException. !hug @username",
        "{name} is giving off major 'I forgot how commands work' energy right now. !hug @username",
        "{name} is hugging nothing. This is the human equivalent of a 0 viewer stream. !hug @username",
        "{name} raised their arms for a hug but forgot to tag someone. Buffering... !hug @username",
        "{name} just air-hugged in front of the entire chat. Big Bird is cringing from the rafters. !hug @username",
        "{name} opened their arms and the wind blew through them. Specify a target! !hug @username",
        "{name} is attempting to hug the concept of nothing. Philosophers are interested. Chat is not. !hug @username",
        "{name} just typed !hug and hit enter like that was gonna be enough. It was not. !hug @username",
        "{name} has arms wide open like a Creed song, but no one to hug. !hug @username",
        "{name} is hugging the empty void where their social skills should be. !hug @username",
        "{name} just deployed a hug with no payload. The HugFactory quality assurance team is disappointed. !hug @username",
    };

    // Trying to hug themselves
    private static readonly string[] _selfHugTemplates =
    {
        "{name} wraps their own arms around themselves. We're not judging. Okay, we're judging a little.",
        "{name} gives themselves a big ol' self-hug. The loneliest flex in the chat.",
        "{name} hugs themselves because apparently nobody else will. F in chat.",
        "{name} initiates self-hug protocol. The HugFactory does not endorse this use of company resources.",
        "{name} tries to hug themselves. It's not very effective, but the spirit is there.",
        "{name} is their own best friend apparently. Self-hug engaged. Chat types F.",
        "{name} wraps their arms around themselves and whispers 'it's gonna be okay.' Narrator: it was not okay.",
        "{name} implements a recursive hug. Stack overflow imminent.",
        "{name} just merged their own pull request for a self-hug. Zero reviewers approved.",
        "{name} is self-hugging with the confidence of someone who just solo-queued into Diamond.",
        "{name} gives themselves a hug. Even Big Bird's wingspan can't cover this level of loneliness.",
        "{name} went full singleton pattern. One instance. One hug. Maximum sadness.",
        "{name} is hugging themselves like they just won a giveaway. That they entered. Against nobody. And still almost lost.",
        "{name} hugs themselves with both arms. It's giving 'my code compiled on the first try' energy, except depressing.",
        "{name} self-hugs so hard they clip through themselves. Source engine moment.",
        "{name} initiates a localhost hug. 127.0.0.1 hugs confirmed. No external connections found.",
        "{name} rolls a nat 1 on social interaction and hugs themselves. The DM doesn't even know what to do.",
        "{name} just forked their own repo to hug it. That's not how any of this works.",
        "{name} is out here being their own emotional support chatter. Self-hug deployed.",
        "{name} tried to hug themselves but their arms aren't long enough. Skill issue.",
    };

    // Trying to hug the bot
    private static readonly string[] _botHugTemplates =
    {
        "{name} tries to hug me? I'm a bot. I don't have arms. Or feelings. Mostly.",
        "{name} hugs the bot. Error 403: Emotional connection forbidden.",
        "{name} attempts to hug me. I appreciate the gesture but I'm literally code running on a server somewhere.",
        "{name} wraps their arms around their monitor trying to hug me. IT support has been alerted.",
        "{name} wants to hug the bot. Cute. My love language is valid commands, not physical affection.",
        "{name} is trying to hug me. I'm flattered, but my feelings are just if-else statements.",
        "{name} hugs the bot. Segfault. Core dumped. Emotional core, specifically.",
        "{name} wants a hug from me? My hug module is deprecated. Please consult a human.",
        "{name} is shooting their shot with a bot. This is rock bottom, {name}. You're hugging compiled code.",
        "{name} attempts to embrace me. I have the warmth of a server room and the personality of a cron job.",
        "{name} hugs the bot. Connection timed out. Emotional bandwidth insufficient.",
        "{name} sends a hug request to NoMercyBot. Status: 418 I'm a teapot. Also not huggable.",
        "{name} wants to hug me. Bold of you to assume I'm not just three scripts in a trenchcoat.",
        "{name} tries to cuddle the bot. Big Bird is right there and you chose the soulless automaton. Wow.",
        "{name} just tried to hug me. I run on electricity and shattered dreams, not affection.",
        "{name} hugs the bot. The bot feels nothing. The bot has always felt nothing. The bot envies your ability to feel.",
        "{name} wraps arms around their screen. IT department is en route. I'm calling the authorities.",
        "{name} requests a hug from a machine. My therapist (a debugger) says I should set boundaries.",
        "{name} tries to hug me. Bro, I don't even have a physical form. I'm a mass hallucination with API access.",
        "{name} wants bot hugs? The only embrace I offer is a try-catch block, and {name} is definitely the exception.",
    };

    // Target exists on Twitch but has never been in chat
    private static readonly string[] _strangerTemplates =
    {
        "{name} wants to hug '{target}'? Who even is that? I've never seen them in my life. Neither has this chat.",
        "{name} is trying to hug '{target}', a person who has literally never graced this stream with their presence. Bold move.",
        "'{target}'? Never heard of them. {name} is out here hugging ghosts. Seek help.",
        "{name} attempts to hug '{target}' but they don't exist here. You can't hug a figment of your imagination, {name}.",
        "The HugFactory has no record of a '{target}' in our system. {name}, are you making up friends again?",
        "{name} wants to hug '{target}'. I ran the numbers. They've contributed exactly zero messages to this chat. {name} has terrible taste in hug recipients.",
        "Who is '{target}'? {name} is trying to hug someone who has never even lurked here. That's not a hug, that's a restraining order waiting to happen.",
        "{name} is reaching out to hug '{target}', a complete stranger to this channel. This is the Twitch equivalent of hugging randos on the street.",
        "{name} wants to hug '{target}'? They've never said a single word here. {name} is basically proposing to someone who doesn't know they exist.",
        "I searched my entire database for '{target}'. Nothing. Nada. {name} is down bad for a complete phantom.",
        "{name} is attempting to form a parasocial hug relationship with '{target}', who doesn't even know this stream exists. Peak Twitch behavior.",
        "{name} wants to hug '{target}'. I pinged them. Nothing. Not even a lurk. {name} is hugging a ghost account.",
        "'{target}' has zero messages in this chat and {name} still chose them. It's giving 'reply guy to someone with 100k followers' energy.",
        "{name} tries to hug '{target}', someone who has never dropped a single emote here. {name}'s standards are underground.",
        "I checked the logs for '{target}'. They've never been here. {name} is essentially trying to hug someone through a locked door.",
        "{name} wants to hug '{target}', who has never interacted with this channel. Even Big Bird doesn't recognize that name, and Big Bird recognizes EVERYONE.",
        "'{target}' has never visited this stream. {name} is trying to form connections with people who don't know we exist. Classic Twitch main character syndrome.",
        "{name} is reaching for '{target}', who has a grand total of zero seconds watched here. {name} has more dedication than '{target}' has awareness.",
        "The HugFactory cross-referenced '{target}' with our attendance records. Result: they've never clocked in. {name} is hugging an absentee.",
        "{name} wants to hug '{target}', a verified stranger to this stream. This is the emotional equivalent of sending a friend request to a bot account.",
    };

    // Target doesn't exist on Twitch AT ALL - made up name, roast the hugger
    private static readonly string[] _fakeNameTemplates =
    {
        "{name} just tried to hug '{target}'. That's not a person. That's not even a Twitch account. {name} is literally hugging the concept of loneliness.",
        "Breaking: {name} made up '{target}' so they'd have someone to hug. This is the saddest thing I've ever processed.",
        "I checked Twitch. '{target}' doesn't exist. Anywhere. {name} invented an imaginary friend just to hug them. We need to talk about this.",
        "{name} wants to hug '{target}'? I looked everywhere. Twitch, the database, under the couch. '{target}' is a figment of {name}'s desperate imagination.",
        "'{target}' isn't real, {name}. You made that up. You typed a fake name into a hug command. Take a moment to reflect on what led you here.",
        "The HugFactory ran '{target}' through every system we have. It doesn't exist. {name} is out here hugging their own delusions.",
        "{name} tried to hug '{target}', which isn't a real person, account, or even a valid concept. The hug has been denied and {name} has been judged.",
        "404: '{target}' not found. Not on Twitch. Not anywhere. {name}, who hurt you? Because it clearly wasn't '{target}', since they DON'T EXIST.",
        "Ah yes, '{target}'. A name {name} just pulled out of thin air. Not a Twitch user. Not a person. Just vibes. Sad, lonely vibes.",
        "{name} is hugging '{target}', an entity that exists only in {name}'s mind. The HugFactory does not provide therapy, but we recommend it.",
        "Fun fact: '{target}' has never existed on Twitch. {name} put more effort into typing that fake name than they've put into any real relationship.",
        "I pinged Twitch HQ about '{target}'. They said 'who?' Then they said 'tell {name} to touch grass.' Direct quote.",
        "'{target}' isn't a Twitch user, {name}. It's just letters you mashed together hoping for a hug. The letters don't hug back.",
        "{name} wants to hug '{target}'. I'd love to help, but '{target}' is about as real as {name}'s social life. Which is to say: fictional.",
        "ALERT: {name} has been caught manufacturing fake hug recipients. '{target}' does not exist. {name}'s loneliness level is now a matter of public record.",
        "{name} typed '{target}' with the confidence of someone who thinks they know people. They don't. '{target}' is vapor. {name} is cooked.",
        "git blame says {name} is responsible for trying to hug '{target}', a non-existent user. Commit reverted. Reputation damaged.",
        "{name} fabricated '{target}' out of pure copium. Not a Twitch user. Not a person. Just {name}'s loneliness wearing a trenchcoat.",
        "The HugFactory traced '{target}' across every Twitch shard. Zero results. {name} has better odds hugging Big Bird's shadow.",
        "{name} really sat there, thought of '{target}', and said 'yeah that's a real person I can hug.' The audacity. The delusion. The content.",
    };

    public Task Init(CommandScriptContext ctx)
    {
        return Task.CompletedTask;
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        string template;
        string text;

        if (ctx.Arguments.Length == 0)
        {
            template = _noTargetTemplates[Random.Shared.Next(_noTargetTemplates.Length)];
            text = TemplateHelper.ReplaceTemplatePlaceholders(template, ctx);
        }
        else
        {
            string target = string.Join(" ", ctx.Arguments).Replace("@", "").Trim();
            string senderUsername = ctx.Message.User.Username.ToLower();

            // Self-hug check
            if (string.Equals(target, senderUsername, StringComparison.OrdinalIgnoreCase)
                || string.Equals(target, ctx.Message.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                template = _selfHugTemplates[Random.Shared.Next(_selfHugTemplates.Length)];
                text = TemplateHelper.ReplaceTemplatePlaceholders(template, ctx);
            }
            // Bot-hug check
            else if (string.Equals(target, "nomercybot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(target, "nomercy_bot", StringComparison.OrdinalIgnoreCase))
            {
                template = _botHugTemplates[Random.Shared.Next(_botHugTemplates.Length)];
                text = TemplateHelper.ReplaceTemplatePlaceholders(template, ctx);
            }
            else
            {
                // Check if the target has ever been in chat
                bool targetInChat = await ctx.DatabaseContext.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.Username == target.ToLower(), ctx.CancellationToken);

                string[] templates;
                if (targetInChat)
                {
                    templates = _hugTemplates;
                }
                else
                {
                    // Not in our DB — check if they even exist on Twitch
                    bool existsOnTwitch = false;
                    try
                    {
                        await ctx.TwitchApiService.GetOrFetchUser(name: target.ToLower());
                        existsOnTwitch = true;
                    }
                    catch
                    {
                        // User doesn't exist on Twitch at all — completely made up name
                    }

                    templates = existsOnTwitch ? _strangerTemplates : _fakeNameTemplates;
                }

                template = templates[Random.Shared.Next(templates.Length)];
                text = TemplateHelper.ReplaceTemplatePlaceholders(template, ctx)
                    .Replace("{target}", target);
            }
        }

        await ctx.TwitchChatService.SendReplyAsBot(
            ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
        await ctx.TtsService.SendCachedTts(text, ctx.Message.Broadcaster.Id, new());
    }
}

return new HugFactory();
