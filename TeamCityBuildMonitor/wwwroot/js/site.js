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
        if (supportsVoice) {
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
                success: function(data) {
                    $.each(data.builds,
                        function(i, build) {
                            var divId = "#BuildDiv-" + build.id;
                            var buildDiv = $(divId);
                            buildDiv.replaceWith(build.content);
                            console.log("buildId:" + build.id + " Message: " + build.brokenBySpeech);
                            if (build.status !== undefined && (build.status === "OK" || build.status === "FAIL")) {
                                doSomeTalking(build.brokenBySpeech);
                            }
                        });
                    $("#last-updated").text(data.updatedText);
                },
                cache: false
            });
        },
        20000);
});