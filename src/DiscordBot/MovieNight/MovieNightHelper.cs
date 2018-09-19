using DiscordBot.MovieNightObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DiscordBot.MovieNight
{
    public class MovieNightHelper
    {
        MovieNightObject _movieNightObject;
        bool _isStarted = false;
        Stopwatch _viewMoviesWatch = new Stopwatch();
        Stopwatch _viewCommandsWatch = new Stopwatch();
        public MovieNightHelper()
        {
            _movieNightObject = new MovieNightObject();
            _movieNightObject.MovieList = new List<Movies>();
            _movieNightObject.UsersVoted = new Dictionary<string, string>();
        }
        public Stopwatch ViewMoviesWatch
        {
            get
            {
                return _viewMoviesWatch;
            }
            set
            {
                _viewMoviesWatch = value;
            }
        }
        public Stopwatch ViewCommandsWatch
        {
            get
            {
                return _viewCommandsWatch;
            }
            set
            {
                _viewCommandsWatch = value;
            }
        }
        public bool IsStarted
        {
            get
            {
                return _isStarted;
            }
            set
            {
                _isStarted = value;
            }
        }
        public MovieNightObject MovieNightObject
        {
            get
            {
                return _movieNightObject;
            }
            set
            {
                _movieNightObject = value;
            }
        }
    }
}
