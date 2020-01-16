$(document).ready(function() {
    $("body").fadeIn(2000);

    var supportsVoice = false;
    var voiceName = "Google US English";
    if ("speechSynthesis" in window) supportsVoice = true;
    var ourVoice = {};

    var getOurVoice = function(voices) {
        $.each(voices, function(i, voice) {
            if (voice.name === voiceName) {
                ourVoice = voice;
            }
        });
    };

    var doSomeTalking = function(text) {
        if (supportsVoice && typeof text !== "undefined" && text != "") {
            var utterance = new SpeechSynthesisUtterance(text);
            utterance.voice = ourVoice;
            window.speechSynthesis.speak(utterance);
        }
    };

    window.speechSynthesis.onvoiceschanged = function () {
        var voices = window.speechSynthesis.getVoices();
        getOurVoice(voices);
    };

    window.initSpeech = function() {
        doSomeTalking("Speech enabled!");
    };

    setInterval(function () {
            $.post({
                url: "/Home/GetBuildsAsync",
                async: true,
                success: function (data) {
                    $(".container").empty();
                    $.each(data.builds,
                        function (i, build) {
                            $(build.content).appendTo(".container");
                            doSomeTalking(build.brokenBySpeech);
                        });
                },
                cache: false
            });
        },
        15000);
});