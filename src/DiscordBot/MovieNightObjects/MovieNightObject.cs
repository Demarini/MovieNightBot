using System;
using System.Collections.Generic;
using System.Text;
namespace DiscordBot.MovieNightObjects
{
    public class MovieNightObject
    {
        public List<Movies> MovieList { get; set; }
        public Dictionary<string, string> UsersVoted { get; set; }
    }
}
