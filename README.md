This repo contains the source code for the Plantronics client for WebRTC Expo 2012 integration demo.

The client code runs on Windows (we tried it with Windows 7) with the current nighly build of Firefox and uses the Plantronics COM interfaces for out of process communications with the Plantronics runtime (Spokes - or PlantronicsURE.exe on the services list).

This application recieves WebRTC media events from Firefox via websockets (The websockets implementation came via Fleck - https://github.com/statianzo/Fleck).  When running the Plantronics client will start a local websockets service running on port 8888. In addition to recieving events from Firefox, the Plantronics application will send events to the Firefox. These events are for media setup (e.g. sample rate, channels) and call control via interactions with the Plantronics headset (button presses, proximity detection, wear state changes).


To build this application you will need Visual Developer Studio 2010 and the Plantronics SDK available at http://developer.plantronics.com.

After the Plantronics SDK is installed, you should be able to build the visual studio project and run the applicaiton. To see the the events from the headset, plug in the dongle, turn on the headset and watch the output windows capture of the headset event stream.


If you want to run the server portion of this applicaiton you will need to get the source code from the webrtc-server project located here: https://github.com/carybran/webrtc-server.  It runs under node.js which can be downloaded here: http://nodejs.org/

For information about how to access the server to run the demo check out the webrtc-server project.



If you want to use the same nightly as we did in the demo do this:

Follow the directions on https://developer.mozilla.org for setting up the firefox build enviroment
Get the latest source code from mozilla

Create a .mozconfig file with the following properties and add it to the mozilla-central directory

mk_add_options MOZ_OBJDIR=@TOPSRCDIR@/obj-ff-dbg
mk_add_options MOZ_MAKE_FLAGS=-j4

ac_add_options --enable-debug
ac_add_options --disable-optimize

ac_add_options --disable-crashreporter

ac_add_options --disable-strip

export MOZ_WEBRTC_TESTS=1


update to the nightly version for this demo: hg update -r ed13d73c61bb

Build the nightly as documented

Kudos to Lewis Collins for his collaboration on this demo.


