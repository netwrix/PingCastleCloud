$(function () {
    $(window).scroll(function () {
        if ($(window).scrollTop() >= 70) {
            $('.information-bar').removeClass('hidden');
            $('.information-bar').fadeIn('fast');
        } else {
            $('.information-bar').fadeOut('fast');
        }
    });
});

document.addEventListener('DOMContentLoaded', function () {

    $('.div_model').on('click', function (e) {
        $('.div_model').not(this).popover('hide');
    });

    new bootstrap.Tooltip(document.body, {
        selector: '.has-tooltip'
    });

});

function getData(dataSelect) {
    try {
        const inlineJsonElement = document.querySelector(
            'script[type="application/json"][data-pingcastle-selector="' + dataSelect + '"]'
        );
        const data = JSON.parse(inlineJsonElement.textContent);
        return data;
    } catch (err) {
        console.error("Couldn't read JSON data from " + dataSelect, err);
    }
}