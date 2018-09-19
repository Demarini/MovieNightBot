using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.MovieNight;
using DiscordBot.MovieNightObjects;

namespace DiscordBot.Services
{
    public class CommandHandlingService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private IServiceProvider _provider;
        Dictionary<ulong, MovieNightHelper> _movieNightDict = new Dictionary<ulong, MovieNightHelper>();
        MovieNightHelper _movieNight = new MovieNightHelper();
        List<string> _authorizedUsers = new List<string>() {};

        public CommandHandlingService(IServiceProvider provider, DiscordSocketClient discord, CommandService commands)
        {
            _discord = discord;
            _commands = commands;
            _provider = provider;

            _discord.MessageReceived += MessageReceived;
        }

        public async Task InitializeAsync(IServiceProvider provider)
        {
            _provider = provider;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());
            // Add additional initialization code here...
        }

        private async Task MessageReceived(SocketMessage rawMessage)
        {
            // Ignore system messages and messages from bots
            if (!(rawMessage is SocketUserMessage message)) return;
            if (message.Source != MessageSource.User) return;


            

            int argPos = 0;
            //if (!message.HasMentionPrefix(_discord.CurrentUser, ref argPos)) return;

            var context = new SocketCommandContext(_discord, message);
            var result = await _commands.ExecuteAsync(context, argPos, _provider);

            if (result.Error.HasValue && 
                result.Error.Value != CommandError.UnknownCommand)
                await context.Channel.SendMessageAsync(result.ToString());


            ParseMovieCommands(rawMessage);
        }
        public async void ParseMovieCommands(SocketMessage message)
        {
            var user = message.Author as SocketGuildUser;
            if (message.Channel.Name != "movie-night" && message.Channel.GetType() != typeof(SocketDMChannel))
            {
                return;
            }
            string[] splits = message.Content.Split(' ');
            string command = splits[0];
            string restOfMessage = String.Join(" ", (splits.Skip(1).Take(splits.Length - 1).ToArray()));

            var user2 = message.Author as SocketGuildUser;
            int userGuildCount = 0;
            SocketGuild guild = null;
            ISocketMessageChannel channel = null;
            ulong currentGuildID = 0;
            if (message.Channel.GetType() == typeof(SocketDMChannel))
            {
                foreach(ulong u in _movieNightDict.Keys)
                {
                    SocketGuild sG = _discord.GetGuild(u);

                    foreach(SocketGuildUser u2 in sG.Users)
                    {
                        if(u2.Id == message.Author.Id)
                        {
                            if (_movieNightDict[sG.Id].IsStarted)
                            {
                                guild = sG;
                                userGuildCount++;
                            }
                        }
                    }
                }
                if (userGuildCount == 0)
                {
                    await message.Channel.SendMessageAsync("You're not in any servers that I am in...how/why are you even messaging me?");
                    return;
                }
                else if (userGuildCount > 1)
                {
                    await message.Channel.SendMessageAsync("You are in multiple servers that I am in and have an active movie vote in progress. I have no way of knowing which one you wish to vote in. Please vote in the server you wish to vote in, or leave the server that you are not actively voting in.");
                    return;
                }
                else
                {
                    foreach (SocketGuildChannel s in guild.Channels)
                    {
                        if (s.Name == "movie-night")
                        {
                            channel = (ISocketMessageChannel)s;
                            currentGuildID = guild.Id;
                            _movieNight = _movieNightDict[guild.Id];
                        }
                    }
                }
            }
            else
            {
                foreach (SocketGuildChannel s in user2.Guild.Channels)
                {
                    if (s.Name == "movie-night")
                    {
                        channel = (ISocketMessageChannel)s;
                        currentGuildID = user2.Guild.Id;
                    }
                }
                if (_movieNightDict.ContainsKey(user2.Guild.Id))
                {
                    _movieNight = _movieNightDict[user2.Guild.Id];
                }
                else
                {
                    _movieNightDict.Add(user2.Guild.Id, new MovieNightHelper());
                    _movieNight = _movieNightDict[user2.Guild.Id];

                }
            }
             
            switch (command)
            {
                case "!StartMovieVoting":
                    if(message.Channel.GetType() == typeof(SocketDMChannel))
                    {
                        return;
                    }
                    if (!CheckForAdmin(message))
                    {
                        return;
                    }
                    if (!_movieNight.IsStarted)
                    {
                        _movieNight.IsStarted = true;
                        await channel.SendMessageAsync("Voting started! Type !Commands for the list of commands available.");
                    }
                    else
                    {
                        await channel.SendMessageAsync("A vote has already been started. You must end the vote before starting a new one.");
                    }
                    break;
                case "!EndMovieVoting":
                    if (message.Channel.GetType() == typeof(SocketDMChannel))
                    {
                        return;
                    }
                    if (!CheckForAdmin(message))
                    {
                        return;
                    }
                    if (_movieNight.IsStarted)
                    {
                        Movies max = new Movies();
                        foreach (Movies m in _movieNight.MovieNightObject.MovieList)
                        {
                            if (m.VoteCount > max.VoteCount)
                            {
                                max = m;
                            }
                        }
                        await channel.SendMessageAsync("Voting has concluded...and the winner is..." + max.Name + "!");
                        _movieNight.IsStarted = false;
                        _movieNightDict.Remove(currentGuildID);
                    }
                    break;
                case "!AddMovie":
                    if (message.Channel.GetType() == typeof(SocketDMChannel))
                    {
                        return;
                    }
                    if (!CheckForAdmin(message))
                    {
                        return;
                    }
                    if (_movieNight.IsStarted)
                    {
                        _movieNight.MovieNightObject.MovieList.Add(new Movies { Id = _movieNight.MovieNightObject.MovieList.Count + 1, Name = restOfMessage, VoteCount = 0 });
                        await channel.SendMessageAsync(restOfMessage + " was added to the voting list!");
                        DisplayMovies(channel, false);
                    }
                    break;
                case "!RemoveMovie":
                    bool found = false; ;
                    foreach (Movies m in _movieNight.MovieNightObject.MovieList)
                    {
                        if (m.Name == restOfMessage || m.Id.ToString() == restOfMessage)
                        {
                            await channel.SendMessageAsync(m.Name + " was removed!");
                            List<string> removeUsers = new List<string>();
                            foreach(string s in _movieNight.MovieNightObject.UsersVoted.Keys)
                            {
                                if(_movieNight.MovieNightObject.UsersVoted[s] == m.Name)
                                {
                                    removeUsers.Add(s);
                                }
                            }
                            for(int i = removeUsers.Count() - 1;i >= 0; i--)
                            {
                                _movieNight.MovieNightObject.UsersVoted.Remove(removeUsers[i]);
                            }
                            
                            _movieNight.MovieNightObject.MovieList.Remove(m);
                            found = true;
                            DisplayMovies(channel, false);
                            break;
                        }
                    }
                    if (!found)
                    {
                        await channel.SendMessageAsync("Movie does not exist!");
                    }
                    break;
                case "!VoteMovie":
                    if (!_movieNight.MovieNightObject.UsersVoted.Keys.Contains(message.Author.Id.ToString()))
                    {
                        bool hasVoted = false;
                        foreach(Movies m in _movieNight.MovieNightObject.MovieList)
                        {
                            if(m.Name == restOfMessage || m.Id.ToString() == restOfMessage)
                            {
                                m.VoteCount++;
                                _movieNight.MovieNightObject.UsersVoted.Add(message.Author.Id.ToString(), m.Name);
                                await channel.SendMessageAsync("Vote was cast for " + m.Name + "!");
                                DisplayMovies(channel, false);
                                hasVoted = true;
                                break;
                            }
                        }
                        if (!hasVoted)
                        {
                            await channel.SendMessageAsync("Movie does not exist!");
                        }
                    }
                    else
                    {
                        await channel.SendMessageAsync("You have already voted for " + _movieNight.MovieNightObject.UsersVoted[message.Author.Id.ToString()] + "!");
                    }
                    break;
                case "!ChangeVote":
                    if (_movieNight.MovieNightObject.UsersVoted.Keys.Contains(message.Author.Id.ToString()))
                    {
                        bool hasVoted = false;
                        if(restOfMessage == _movieNight.MovieNightObject.UsersVoted[message.Author.Id.ToString()])
                        {
                            await channel.SendMessageAsync("You have already voted for " + restOfMessage + "!");
                            break;
                        }
                        foreach (Movies m in _movieNight.MovieNightObject.MovieList)
                        {
                            if (m.Name == restOfMessage || m.Id.ToString() == restOfMessage)
                            {
                                await channel.SendMessageAsync("Vote was changed from " + _movieNight.MovieNightObject.UsersVoted[message.Author.Id.ToString()] + " to " + m.Name + ".");
                                foreach (Movies m2 in _movieNight.MovieNightObject.MovieList)
                                {
                                    if(m2.Name == _movieNight.MovieNightObject.UsersVoted[message.Author.Id.ToString()])
                                    {
                                        m2.VoteCount--;
                                    }
                                }

                                _movieNight.MovieNightObject.UsersVoted[message.Author.Id.ToString()] = m.Name;
                                m.VoteCount++;
                               
                                DisplayMovies(channel, false);
                                hasVoted = true;
                                break;
                            }
                        }
                        if (!hasVoted)
                        {
                            await channel.SendMessageAsync("Movie does not exist!");
                        }
                    }
                    else
                    {
                        await channel.SendMessageAsync("You have no voted yet!");
                    }
                    break;
                case "!ViewMovieList":
                    if (_movieNight.IsStarted)
                    {  
                        DisplayMovies(channel, true);
                    }
                    break;
                case "!Commands":
                    if (_movieNight.IsStarted)
                    {
                        DisplayCommands(channel);
                    }
                    break;
            }
        }
        public async void DisplayCommands(ISocketMessageChannel channel)
        {
            string startMovie = "!StartMovieVoting - Initializes a new movie vote. You must use this prior to any of the other commands. Only administrators can use this command.";
            string endMovie = "!EndMovie - Ends the movie vote. Only administrators can use this command.";
            string addMovie = "!AddMovie (Name) - Adds a new movie to vote on. Only administrators can use this command.";
            string removeMovie = "!RemoveMovie (ID/Name) - Removes a movie from the list. Only administrators can use this command.";
            string voteMovie = "!VoteMovie (ID/Name) - Adds your vote to a movie. Users can only vote once. Use the movie ID or exact name to vote. Anyone can use this command.";
            string changeVote = "!ChangeVote (ID/Name) - Changes your vote to this movie. Anyone can use this command.";
            string viewMovieList = "!ViewMovieList - Views this list of movies and the amount of votes they have. Anyone can use this command(has a 10 second cooldown).";

            if ((_movieNight.ViewCommandsWatch.ElapsedMilliseconds == 0 || _movieNight.ViewCommandsWatch.ElapsedMilliseconds > 10000))
            {
                _movieNight.ViewCommandsWatch.Start();
                string message2 = startMovie+ "\n" + endMovie+ "\n" + addMovie+ "\n" + removeMovie + "\n" + voteMovie+ "\n" + changeVote+ "\n" + viewMovieList;

               await channel.SendMessageAsync(message2);
                
                _movieNight.ViewCommandsWatch.Reset();
                _movieNight.ViewCommandsWatch.Stop();
                _movieNight.ViewCommandsWatch.Start();
            }

            
        }
        public async void DisplayMovies(ISocketMessageChannel channel, bool fromCommand)
        {
            if ((_movieNight.ViewMoviesWatch.ElapsedMilliseconds == 0 || _movieNight.ViewMoviesWatch.ElapsedMilliseconds > 10000) || !fromCommand)
            {
                _movieNight.ViewMoviesWatch.Start();
                _movieNight.MovieNightObject.MovieList = _movieNight.MovieNightObject.MovieList.OrderByDescending(x => x.VoteCount).ToList();
                string movieMessage = "**Current Movies**";
                foreach (Movies m in _movieNight.MovieNightObject.MovieList)
                {
                    movieMessage = movieMessage + "\n-------------------------------\nMovie Name: *" + m.Name + "*\n" + "Voting ID: " + m.Id.ToString() + "\n" + "Vote Count: " + m.VoteCount.ToString();
                }
                await channel.SendMessageAsync(movieMessage);
                _movieNight.ViewMoviesWatch.Reset();
                _movieNight.ViewMoviesWatch.Stop();
                _movieNight.ViewMoviesWatch.Start();
            }
        }
        public bool CheckForAdmin(SocketMessage message)
        {
            var user = message.Author as SocketGuildUser;
            if (user.GuildPermissions.Administrator == true)
            {
                return true; 
            }
            else
            {
                return false;
            }
        }
    }
}
