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
    $("#transcriptText").html("");
    document.getElementById('inputTurn').value = "";
    document.getElementById("chatGroup").removeAttribute("hidden");
}

var needToStop = false;
function SendTurn() {
    var contiGen = false;
    needToStop = true;
    $("#btnNextTurn").attr("value", "Stop");
    var sendAjax = function () {
        var transcriptEL = document.getElementById("transcriptText");
        var transcript = transcriptEL.innerHTML;

        var inputTurnEL = document.getElementById("inputTurn");
        var inputTurn = inputTurnEL.value;

        $.ajax({
            type: "POST",
            url: "/Home/SendTurn",
            dataType: "json",
            data: { "transcript": transcript, "inputTurn": inputTurn, "contiGen": contiGen && !needToStop },
            beforeSend: function () {
                document.getElementById("dotdotdot").removeAttribute("hidden");

                if ((inputTurn != null && inputTurn != "")) {
                    needToStop = false;
                }
            },
            success: function (result) {

                const parts = result.output.split('\t');
                if (parts[0].endsWith(" EOS")) {
                    parts[0] = parts[0].substring(0, parts[0].length - 4);
                    needToStop = true;
                }


                $("#transcriptText").html(parts[0]);
                document.getElementById('inputTurn').value = "";
                document.getElementById("dotdotdot").setAttribute("hidden", "hidden");

                if (needToStop == false) {
                    contiGen = true;
                    sendAjax();
                }
                else {
                    $("#btnNextTurn").attr("value", "Start");
                }
            },
            error: function (err) {
                $("#btnNextTurn").attr("value", "Start");
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

function RefreshTurn(idx) {

    var contiGen = false;
    needToStop = false;

    $("#btnNextTurn").attr("value", "Stop");
    var sendAjaxRegen = function () {
        var transcriptEL = document.getElementById("transcriptText");
        var transcript = transcriptEL.innerHTML;

        $.ajax({
            type: "POST",
            url: "/Home/RefreshTurn",
            dataType: "json",
            data: { "transcript": transcript, "contiGen": contiGen && !needToStop, "idx": idx },
            beforeSend: function () {
                document.getElementById("dotdotdot").removeAttribute("hidden");
            },
            success: function (result) {
                const parts = result.output.split('\t');
                if (parts[0].endsWith(" EOS")) {
                    parts[0] = parts[0].substring(0, parts[0].length - 4);
                    needToStop = true;
                }

                $("#transcriptText").html(parts[0]);
                document.getElementById("dotdotdot").setAttribute("hidden", "hidden");

                if (needToStop == false) {
                    contiGen = true;
                    sendAjaxRegen();
                }
                else {
                    $("#btnNextTurn").attr("value", "Start");
                }
            },
            error: function (err) {
                $("#btnNextTurn").attr("value", "Start");
                document.getElementById("dotdotdot").setAttribute("hidden", "hidden");
            }
        });
    };

    sendAjaxRegen();
}
