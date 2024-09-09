// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

Array.prototype.myJoin = function (seperator, start, end) {
    if (!start) start = 0;
    if (!end) end = this.length - 1;
    end++;
    return this.slice(start, end).join(seperator);
};

function UpdateInfo() {

    //var Description = document.getElementById('Description').value;

    //if (Description == null || Description == "") {
    //    Description = document.getElementById('Description').placeholder;
    //}
  
    $("#transcriptText").html("");
    document.getElementById('inputTurn').value = "";
    document.getElementById("chatGroup").removeAttribute("hidden");
}

var needToStop = false;

function SendTurn() {

    var contiGen = false;

    var sendAjax = function () {
        var transcriptEL = document.getElementById("transcriptText");
        var transcript = transcriptEL.innerHTML;

        var inputTurnEL = document.getElementById("inputTurn");
        var inputTurn = inputTurnEL.value;

        $.ajax({
            type: "POST",
            url: "/Home/SendTurn",
            dataType: "json",
            data: { "transcript": transcript, "inputTurn": inputTurn, "contiGen": contiGen},
            beforeSend: function () {
                $("#regenerateTurn").attr("disabled", true);
                document.getElementById("dotdotdot").removeAttribute("hidden");

                if (contiGen == false && (inputTurn == null || inputTurn == "")) {
                    needToStop = true;
                }
                else {
                    needToStop = false;
                }
            },
            success: function (result) {

                const parts = result.output.split('\t');
                var EOS = false;
                if (parts[0].endsWith(" EOS")) {
                    EOS = true;
                    parts[0] = parts[0].substring(0, parts[0].length - 4);
                }


                $("#transcriptText").html(parts[0]);
                $("#regenerateTurn").attr("disabled", false);
                document.getElementById('inputTurn').value = "";
                document.getElementById("dotdotdot").setAttribute("hidden", "hidden");

                if (EOS == false && needToStop == false) {
                    contiGen = true;
                    sendAjax();
                }
            },
            error: function (err) {
                $("#btnNextTurn").attr("disabled", false);
                $("#regenerateTurn").attr("disabled", false);
                document.getElementById("dotdotdot").setAttribute("hidden", "hidden");
            }
        });
    };

    sendAjax();

}


function RemoveTurn(idx) {

    var sendAjaxRemove = function () {
        var transcriptEL = document.getElementById("transcriptText");
        var transcript = transcriptEL.innerHTML;

        $.ajax({
            type: "POST",
            url: "/Home/RemoveTurn",
            dataType: "json",
            data: { "transcript": transcript, "idx": idx },
            beforeSend: function () {
            },
            success: function (result) {
                const parts = result.output.split('\t');
                $("#transcriptText").html(parts[0]);
            },
            error: function (err) {
            }
        });
    };

    sendAjaxRemove();
}

function RegenerateTurn() {

    var contiGen = false;

    var sendAjaxRegen = function () {
        var transcriptEL = document.getElementById("transcriptText");
        var transcript = transcriptEL.innerHTML;

        $.ajax({
            type: "POST",
            url: "/Home/RegenerateTurn",
            dataType: "json",
            data: { "transcript": transcript, "contiGen": contiGen },
            beforeSend: function () {
                $("#btnNextTurn").attr("disabled", true);
                $("#regenerateTurn").attr("disabled", true);
                document.getElementById("dotdotdot").removeAttribute("hidden");
            },
            success: function (result) {
                const parts = result.output.split('\t');
                var EOS = false;
                if (parts[0].endsWith(" EOS")) {
                    EOS = true;
                    parts[0] = parts[0].substring(0, parts[0].length - 4);
                }

                $("#transcriptText").html(parts[0]);
                $("#btnNextTurn").attr("disabled", false);
                $("#regenerateTurn").attr("disabled", false);
                document.getElementById("dotdotdot").setAttribute("hidden", "hidden");

                if (EOS == false) {
                    contiGen = true;
                    sendAjaxRegen();
                }
            },
            error: function (err) {
                $("#btnNextTurn").attr("disabled", false);
                $("#regenerateTurn").attr("disabled", false);
                document.getElementById("dotdotdot").setAttribute("hidden", "hidden");
            }
        });
    };

    sendAjaxRegen();
}
