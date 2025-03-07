document.addEventListener('DOMContentLoaded', processEnableAfterLoadingElements);

function processEnableAfterLoadingElements() {
    Array.from(document.querySelectorAll('.enable-after-loading'))
        .forEach(x => {
            x.classList.remove('wait-cursor');
        });

    gsap.to(".enable-after-loading", {
        duration: .5, opacity: 1, onComplete: function () {
            this.targets().forEach(x => x.style.pointerEvents = 'auto');
        }
    });
}

function debounce(func, timeout = 500) {
    let timer;
    return (...args) => {
        clearTimeout(timer);
        timer = setTimeout(() => { func.apply(this, args); }, timeout);
    };
}

function sortString(dataIdentifier, ascending = true) {
    var list = document.querySelector('.content-list-container');

    Array.from(document.querySelectorAll('.content-list-item-container'))
        .sort((a, b) => {
            const valueA = a.getAttribute(dataIdentifier);
            const valueB = b.getAttribute(dataIdentifier);
            if (valueA > valueB) return ascending ? 1 : -1;
            if (valueA < valueB) return ascending ? -1 : 1;
            return 0;
        })
        .forEach(node => list.appendChild(node));
}

function sortCreatedAscending() {
    sortString('data-created', true);
}

function sortCreatedDescending() {
    sortString('data-created', false);
}

function sortUpdatedAscending() {
    sortString('data-updated', true);
}

function sortUpdatedDescending() {
    sortString('data-updated', false);
}

function sortTitleAscending() {
    sortString('data-title', true);
}

function sortTitleDescending() {
    sortString('data-title', false);
}

function sortNumber(dataIdentifier, ascending = true) {
    var list = document.querySelector('.content-list-container');

    Array.from(document.querySelectorAll('.content-list-item-container'))
        .sort((a, b) => {
            const valueA = parseFloat(a.getAttribute(dataIdentifier));
            const valueB = parseFloat(b.getAttribute(dataIdentifier));
            return ascending ? valueA - valueB : valueB - valueA;
        })
        .forEach(node => list.appendChild(node));
}

function sortDistanceAscending() {
    sortNumber('data-distance', true);
}

function sortDistanceDescending() {
    sortNumber('data-distance', false);
}

function sortClimbAscending() {
    sortNumber('data-climb', true);
}

function sortClimbDescending() {
    sortNumber('data-climb', false);
}

function sortMaxElevationAscending() {
    sortNumber('data-max-elevation', true);
}

function sortMaxElevationDescending() {
    sortNumber('data-max-elevation', false);
}

function searchContent() {

    gsap.to(".content-list-container", { duration: .5, opacity: 0, display: "none" });

    var filterText = document.querySelector('#userSearchText').value.toUpperCase();

    var contentTypes = Array.from(document.querySelectorAll('.content-list-filter-checkbox'))
        .filter(x => x.checked).map(x => x.value);

    var trailTrueFilterTypes = Array.from(document.querySelectorAll('.trail-list-true-filter-checkbox'))
        .filter(x => x.checked).map(x => x.value);

    var trailFalseFilterTypes = Array.from(document.querySelectorAll('.trail-list-false-filter-checkbox'))
        .filter(x => x.checked).map(x => x.value);

    var trailFilterCommonValues = trailTrueFilterTypes.filter(value => trailFalseFilterTypes.includes(value));
    trailTrueFilterTypes = trailTrueFilterTypes.filter(value => !trailFilterCommonValues.includes(value));
    trailFalseFilterTypes = trailFalseFilterTypes.filter(value => !trailFilterCommonValues.includes(value));

    var trailLocationFilterDropdown = document.querySelector('#trail-location-filter-dropdown');
    var trailLocationFilter = trailLocationFilterDropdown ? trailLocationFilterDropdown.value : '';

    var mainSiteFeedFilter = Array.from(document.querySelectorAll('.site-main-feed-filter-checkbox')).some(x => x.checked);

    var contentDivs = Array.from(document.querySelectorAll('.content-list-item-container'));

    // Loop through all list items, and hide those who don't match the search query
    for (var i = 0; i < contentDivs.length; i++) {
        var loopDiv = contentDivs[i];

        var divMainSiteFeed = loopDiv.getAttribute('data-site-main-feed');

        if (mainSiteFeedFilter && divMainSiteFeed === 'false') {
            loopDiv.classList.remove("shown-list-item");
            loopDiv.classList.add("hidden-list-item");
            continue;
        }

        var divDataContentType = loopDiv.getAttribute('data-content-type');

        if (contentTypes.length && !contentTypes.includes(divDataContentType)) {
            loopDiv.classList.remove("shown-list-item");
            loopDiv.classList.add("hidden-list-item");
            continue;
        }

        if (trailLocationFilter !== '') {
            if (loopDiv.getAttribute('data-trail-location-area') !== trailLocationFilter) {
                loopDiv.classList.remove("shown-list-item");
                loopDiv.classList.add("hidden-list-item");
                continue;
            }
        }

        function checkDataAttributes(elementToCheck, filerDataTypes, filterValue) {
            for (let type of filerDataTypes) {
                var dataAttribute = `data-${type}`;
                if (elementToCheck.getAttribute(dataAttribute) === filterValue) {
                    console.log(`elementToCheck has ${dataAttribute} set to true`);
                    return true;
                } else {
                    elementToCheck.classList.remove("shown-list-item");
                    elementToCheck.classList.add("hidden-list-item");
                    return false;
                }
            }
            return true;
        }

        if (!checkDataAttributes(loopDiv, trailTrueFilterTypes, "true")) continue;
        if (!checkDataAttributes(loopDiv, trailFalseFilterTypes, "false")) continue;

        var divDataText = loopDiv.getAttribute('data-title').concat(
            loopDiv.getAttribute('data-summary'),
            loopDiv.getAttribute('data-tags').replace(/-/g, ' ')).toUpperCase();

        if (filterText == null || filterText.trim() === '') {
            loopDiv.classList.remove("hidden-list-item");
            loopDiv.classList.add("shown-list-item");
        }
        else if (divDataText.indexOf(filterText) > -1) {
            loopDiv.classList.remove("hidden-list-item");
            loopDiv.classList.add("shown-list-item");
        } else {
            loopDiv.classList.remove("shown-list-item");
            loopDiv.classList.add("hidden-list-item");
        }
    }

    document.querySelectorAll(".shown-list-item").forEach(x => x.style.display = '');
    document.querySelectorAll(".hidden-list-item").forEach(x => x.style.display = 'none');

    gsap.to(".content-list-container", { duration: .5, opacity: 1, display: "" });
}

const processSearchContent = debounce(() => searchContent());